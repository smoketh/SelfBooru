using LiteDB;

namespace SelfBooru
{
    public partial class App : Application
    {
        internal LiteDatabase db;
        internal LiteCollection<TaggedDiskImage> images;
        internal LiteCollection<Tag> tags;
        internal ImageCache cache;
        private static AppPreferences preferences = AppPreferences.Load();
        internal Dictionary<string, ReadOnlyMemory<byte>> thumbsDict=new Dictionary<string, ReadOnlyMemory<byte>>();
        internal ImageSource noImage;
        internal bool IsScanning { get; private set; } = false;
        internal static SelfBooru.AppPreferences Preferences { get => preferences; }

        internal void LoadThumbs()
        {
            if(!File.Exists("thumbs.pack"))
            {

                return;
            }
            var bytes=File.ReadAllBytes("thumbs.pack");
            thumbsDict = MessagePack.MessagePackSerializer.Deserialize<Dictionary<string, ReadOnlyMemory<byte>>>(bytes);


        }
        internal void SaveThumbs()
        {
            var bytes = MessagePack.MessagePackSerializer.Serialize(thumbsDict);
            File.WriteAllBytes("thumbs.pack", bytes);
        }
        public App()
        {
            InitializeComponent();
            if(!File.Exists("filenotfound.scale-100.png"))
            {
                throw new Exception("filenotfound.scale-100.png ironically not found");
            }
            noImage = ImageSource.FromFile("filenotfound.scale-100.png");
            //AppPreferences.Load();
            cache= new ImageCache();
            LoadThumbs();
            System.Diagnostics.Debug.WriteLine($"Outputdir set to {Preferences.OutputDir}");
            string appDir = AppContext.BaseDirectory;
            string dbPath = Path.Combine(appDir, "selfbooru_data.db");
            //System.Diagnostics.Debug.WriteLine(FileSystem.AppDataDirectory);
            try
            {
                //string dbPath = Path.Combine("selfbooru_data.db");
                // Use 'using' or ensure disposal later
                db = new LiteDatabase(dbPath);

                // Get collections
                images = (LiteCollection<TaggedDiskImage>)db.GetCollection<TaggedDiskImage>("images");
                tags = (LiteCollection<Tag>)db.GetCollection<Tag>("tags");

                // --- Define Indexes ---
                // Ensure unique index on md5Hash for fast lookups/deduplication
                images.EnsureIndex(x => x.md5Hash, false); // 'true' means unique

                // Ensure index on filePath
                images.EnsureIndex(x => x.filePath);

                // Ensure index on the tags list for querying images by tag ID
                // This creates an index on each ObjectId element within the tags list
                images.EnsureIndex("$.tags[*]"); // JsonPath syntax for indexing array elements

                // Ensure unique index on tag text
                tags.EnsureIndex(x => x.text, true); // 'true' means unique

                // -----------------------

                //MainPage = new AppShell();
            }
            catch (Exception ex)
            {
                // Handle initialization errors
                System.Diagnostics.Debug.WriteLine($"Error initializing database: {ex}");
                // Depending on your error handling strategy, you might want to display an alert or close the app
            }

            
            
        }


        protected override Window CreateWindow(IActivationState? activationState)
        {
            var nWin= new Window(new NavigationPage(new MainPage() ));
            nWin.Destroying += NWin_Destroying;
            return nWin;
        }

        private void NWin_Destroying(object? sender, EventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine($"Error initializing database: {ex}");
            //SaveThumbs();
            Preferences.Save();
            //throw new NotImplementedException();
        }
    }
}