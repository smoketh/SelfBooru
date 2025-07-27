using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using LiteDB;
using Microsoft.Maui.Controls;

namespace SelfBooru
{
    public class MainPageViewModel : BindableObject
    {
        private readonly LiteDatabase _db;
        private const int PageSize = 64;
        private readonly ImageCache cache;
        public ObservableCollection<string> Suggestions { get; } = new();
        public bool ShowSuggestions { get; set; }

        private ObservableCollection<ImageItem> _pagedImages = new();
        public ObservableCollection<ImageItem> PagedImages
        {
            get => _pagedImages;
            set
            {
                _pagedImages = value;
                OnPropertyChanged(nameof(PagedImages));
            }
        }

        private string _searchText="";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
            }
        }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage != value)
                {
                    _currentPage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PageDisplayText));
                    OnPropertyChanged(nameof(CanPrevPage));
                    OnPropertyChanged(nameof(CanNextPage));
                    OnPropertyChanged(nameof(HasMultiplePages));
                }
            }
        }
        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            set
            {
                if (_totalPages != value)
                {
                    _totalPages = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PageDisplayText));
                    OnPropertyChanged(nameof(HasMultiplePages));
                }
            }
        }

        public string PageDisplayText => $"Page {_currentPage} of {_totalPages}";

       /* private string _metadata;
        public string MetaData
        {
            get { return _metadata; }
            set { _metadata = value; }
        }*/
        public ICommand GoToFirstPageCommand { get; }
        public ICommand GoToLastPageCommand { get; }




        public bool HasMultiplePages => TotalPages > 2;
        public bool CanPrevPage => _currentPage > 1;
        public bool CanNextPage => _currentPage < _totalPages;

        public ICommand PrevPageCommand { get; }
        public ICommand NextPageCommand { get; }

        public ICommand TryPreviousImageCommand { get;  }
        public ICommand TryNextImageCommand { get; }
        /*public string SelectedImagePath
        {
            get => _selectedImagePath;
            set { _selectedImagePath = value; OnPropertyChanged(); }
        }
        private string _selectedImagePath = string.Empty;*/

        private ImageItem _selectedImage;
        public ImageItem SelectedImage
        {
            get { return  _selectedImage; }
            set { _selectedImage = value; OnPropertyChanged(); }
        }
        public bool suppressSuggestions = false;
        public bool IsPreviewVisible
        {
            get => _isPreviewVisible;
            set { _isPreviewVisible = value; OnPropertyChanged(); }
        }
        private bool _isPreviewVisible;

        public bool IsMetadataVisible
        {
            get => _isMetadataVisible;
            set { _isMetadataVisible = value; OnPropertyChanged(); }
        }
        private bool _isMetadataVisible;

        public ICommand ClosePreviewCommand { get; }
        public ICommand ShowMetadataCommand { get; }

        public ICommand HideMetadataCommand { get; }
        public ICommand RevealFileInExplorerCommand { get; }
        private ObservableCollection<string> _relatedTags = new();
        public ObservableCollection<string> RelatedTags 
        { get => _relatedTags;
          set
            {
                _relatedTags = value;
                OnPropertyChanged(nameof(RelatedTags));
            }
        }
        public bool RelatedTagsAvailable 
        {
            get 
            {
                return !ShowSuggestions && _relatedTags != null && _relatedTags.Count > 0;
            }
        }
        
        LiteCollection<TaggedDiskImage> images;
        LiteCollection<Tag> tags;
        Dictionary<string, ReadOnlyMemory<byte>> thumbsDict;
        ImageSource noImage;
        public MainPageViewModel()
        {
            
            var app = Application.Current as App;
            var db = app?.db ?? throw new InvalidOperationException("Application has no db loaded");
            cache = app?.cache ?? throw new InvalidOperationException("Application has no cache mounted");
            thumbsDict = app?.thumbsDict ?? throw new InvalidOperationException("Application has no thumbs somehow");
            noImage = app?.noImage ?? throw new FileNotFoundException("noImage doesn't exist");
            images = (LiteCollection<TaggedDiskImage>)db.GetCollection<TaggedDiskImage>("images");
            tags = (LiteCollection<Tag>)db.GetCollection<Tag>("tags");
            // Initialize LiteDB path same as App
            //var dbPath = Path.Combine(AppContext.BaseDirectory, "selfbooru_data.db");
            _db = db;
            //_db = app?.db ?? throw new InvalidOperationException("Application has no db loaded");
            var imagesColl = _db.GetCollection<TaggedDiskImage>("images");
            var tagColl = _db.GetCollection<Tag>("tags");
            PrevPageCommand = new Command(() => GoToPage(_currentPage - 1));
            NextPageCommand = new Command(() => GoToPage(_currentPage + 1));
            GoToFirstPageCommand = new Command(() => GoToPage(1));
            GoToLastPageCommand = new Command(() => GoToPage(TotalPages));
            ClosePreviewCommand = new Command(() => IsPreviewVisible = false);
            ShowMetadataCommand = new Command(ShowMetadata);
            HideMetadataCommand = new Command(HideMetadata);
            TryPreviousImageCommand= new Command(TryPreviousImage);
            TryNextImageCommand= new Command(TryNextImage);
            RevealFileInExplorerCommand = new Command(RevealInExplorer);
        }

        internal void AddTag(string tag)
        {
            if(!ShowSuggestions)
            if (!string.IsNullOrWhiteSpace(tag))
            {
                    _searchText = (_searchText + " " + tag).Trim();
                    suppressSuggestions = true;
                    OnPropertyChanged(nameof(SearchText));
                    //SearchText = (SearchText + " " + tag).Trim();
                }
        }

        private void TryPreviousImage()
        {
            if(SelectedImage.PreviousRoute!= PathRoute.None)
            {
                if(SelectedImage.PreviousRoute == PathRoute.Image)
                {
                    SelectedImage = PagedImages[SelectedImage.IndexInTable - 1];
                }
                else //page
                {
                    //IsPreviewVisible = false;
                    GoToPage(_currentPage - 1);
                    SelectedImage = PagedImages[PagedImages.Count-1];
                    
                }
            }
        }
        private void TryNextImage()
        {
            if (SelectedImage.NextRoute != PathRoute.None)
            {
                if (SelectedImage.NextRoute== PathRoute.Image)
                {
                    SelectedImage = PagedImages[SelectedImage.IndexInTable + 1];
                }
                else //page
                {
                    //IsPreviewVisible = false;
                    GoToPage(_currentPage + 1);
                    SelectedImage = PagedImages[0];

                }
            }
        }
        private void GoToPage(int page)
        {
            if (page < 1) page = 1;
            if (page > TotalPages) page = TotalPages;

            CurrentPage = page;
            LoadPage(); // reloads images
        }
        private void ShowMetadata()
        {
            //_db.GetCollection<TaggedDiskImage>("images");
            //_selectedImagePath
            // Example: Open metadata popup or alert
            //_metadata = images.FindOne(x => x.filePath == _selectedImagePath).metadata ?? "No metadata available.";
            IsMetadataVisible = true;
            
            OnPropertyChanged(nameof(IsMetadataVisible));
            //OnPropertyChanged(nameof(MetaData));

            /*Application.Current.MainPage.DisplayAlert(
                "Metadata",
                images.FindOne(x => x.filePath == _selectedImagePath).metadata ?? "No metadata available.",
                "OK"
            );*/
        }
        private void HideMetadata()
        {
            IsMetadataVisible = false;
            OnPropertyChanged(nameof(IsMetadataVisible));
        }
        public void UpdateSuggestions()
        {
            var parts = SearchText?.Split(' ') ?? Array.Empty<string>();
            var current = parts.LastOrDefault() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(current))
            {
                ShowSuggestions = false;
                OnPropertyChanged(nameof(ShowSuggestions));
                OnPropertyChanged(nameof(RelatedTagsAvailable));
                return;
            }

            // Query tags collection for matches
            var tags = (LiteCollection<Tag>)_db.GetCollection<Tag>("tags");
            var matches = tags.Query()
                              .Where(x => x.text.StartsWith(current, StringComparison.OrdinalIgnoreCase))
                              .Limit(10).ToList().Select(x => x.text);
                              //.Select(x => x.text)
                              //.ToList();

            Suggestions.Clear();
            foreach (var tag in matches)
                Suggestions.Add(tag);

            ShowSuggestions = Suggestions.Any();
            OnPropertyChanged(nameof(ShowSuggestions));
            OnPropertyChanged(nameof(RelatedTagsAvailable));
        }

        public void ApplySuggestion(string selected)
        {
            var parts = SearchText.Split(' ').ToList();
            if (parts.Count > 0)
                parts[parts.Count - 1] = selected;
            else
                parts.Add(selected);

            SearchText = string.Join(' ', parts);
            ShowSuggestions = false;
            OnPropertyChanged(nameof(ShowSuggestions));
            OnPropertyChanged(nameof(RelatedTagsAvailable));

            //ExecuteSearch();
        }

        public void SCH()
        {
            OnPropertyChanged(nameof(ShowSuggestions));
            OnPropertyChanged(nameof(RelatedTagsAvailable));
        }

        public void ExecuteSearch()
        {
            _currentPage = 1;
            LoadPage();
        }

        private void ChangePage(int newPage)
        {
            if (newPage < 1 || newPage > _totalPages) return;
            _currentPage = newPage;
            LoadPage();
        }

        public void RevealInExplorer()
        {
            if (SelectedImage == null) return;
            string filePath = SelectedImage.ImagePath;
            if (File.Exists(filePath))
            {
                string argument = $"/select,\"{filePath}\"";
                System.Diagnostics.Process.Start("explorer.exe", argument);
            }
        }

        private void LoadPage()
        {
            System.Diagnostics.Debug.WriteLine($"trying to process searchtext {SearchText}");
            var parts = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var include = parts.Where(t => !t.StartsWith("-", StringComparison.Ordinal)).ToArray();
            var exclude = parts.Where(t => t.StartsWith("-", StringComparison.Ordinal)).Select(t => t[1..]).ToArray();

            var tagColl = tags;
            var imagesColl = images;

            // Build query: include tags AND, exclude tags NOT
            var includeIds = include
                .Select(t => tagColl.FindOne(x => x.text == t)?.Id)
                .Where(id => id != null)
                //.Select(id => id!.Value)
                .ToList();
            var excludeIds = exclude
                .Select(t => tagColl.FindOne(x => x.text == t)?.Id)
                .Where(id => id != null)
                //.Select(id => id!.Value)
                .ToList();
            System.Diagnostics.Debug.WriteLine($"trying to process searchtext {includeIds[0]}");
            // Query images by tags array
            var query = imagesColl.Query();
            query.OrderByDescending(x=>x.created);
            foreach (var id in includeIds)
                query = query.Where(x => x.tags!=null && x.tags.Contains(id));
            foreach (var id in excludeIds)
                query = query.Where(x => x.tags!=null && !x.tags.Contains(id));

            //query = query.Where(x => File.Exists(x.filePath));

            var totalCount = query.Count();
            _totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

            var skip = (_currentPage - 1) * PageSize;

            var totalDocs = query.ToList();
            //if (parts != null && parts.Length > 0)//i'll be fucked if i go through whole databse on a whim like that
            //{
                Dictionary<ObjectId, int> tagInstances = new Dictionary<ObjectId, int>();
                foreach (var doc in totalDocs)
                {
                    if (doc.tags == null) continue;
                    //if (!File.Exists(doc.filePath)) continue;
                    //for(int q=0; q<doc.tags.Count; q++)
                    foreach (var tag in doc.tags)
                    {
                        if (tagInstances.ContainsKey(tag))
                        {
                            tagInstances[tag] = tagInstances[tag] + 1;
                        }
                        else
                            tagInstances[tag] = 1;
                    }
                }
                var nList = tagInstances.OrderByDescending(x => x.Value).Take(20).ToList();
                List<string> finalTagCloud = new List<string>();
                //now let's build collection out of that
                foreach (var fc in nList)
                {
                    var ctgtxt = tagColl.FindOne(x => x.Id == fc.Key);
                    if (ctgtxt == null) continue;
                    var txt = ctgtxt.text;
                    if(!include.Contains(txt))
                        finalTagCloud.Add(txt);
                }
                RelatedTags = new ObservableCollection<string>(finalTagCloud);
            //}
            //else
            //    RelatedTags.Clear();
            OnPropertyChanged(nameof(RelatedTagsAvailable)); 
                //Markdig.Helpers.OrderedList

                var pageDocs = query.Skip(skip).Limit(PageSize).ToList();
            List<ImageItem> fList = new List<ImageItem>();
            for(int q=0; q<pageDocs.Count; q++)
            {
                var pRoute = PathRoute.None;
                if(q == 0)
                {
                    if(_currentPage>1) pRoute=PathRoute.Page;
                }
                else
                {
                    pRoute = PathRoute.Image;
                }
                var nRoute = PathRoute.None;
                if(q== pageDocs.Count-1)
                {
                    if (_currentPage < _totalPages) nRoute = PathRoute.Page;
                }
                else
                {
                    nRoute = PathRoute.Image;
                }
                //var fpFinal = pageDocs[q].filePath;
                //var thumbfinal= pageDocs[q].thumbnail;//bytes
                /*if (!File.Exists(fpFinal))
                {
                    fpFinal = "filenotfound.png";
                    thumbfinal = fpFinal;
                }*/
                var metaFinal = pageDocs[q].metadata;
                /*if (pageDocs[q].tags!=null)
                {
                    var eStr = "";
                    foreach(var tag in pageDocs[q].tags!)
                    {
                        var ftag=tagColl.FindOne(x => x.Id == tag)!.text;
                        eStr += ftag + ", ";
                    }
                    metaFinal = $"Assigned tags:\n{eStr}\n\nMetadata:\n{metaFinal}";
                }*/
                var imageBytes = cache.GetOrAdd(pageDocs[q].Id.ToString()+"_full", () => File.ReadAllBytes(pageDocs[q].filePath));
                var imageSource = ImageSource.FromStream(() => new MemoryStream(imageBytes));

                //var thumbBytes = pageDocs[q].thumbnail;//cache.GetOrAdd(pageDocs[q].Id.ToString()+"_thumb", () => pageDocs[q].thumbnail );
                var thumbSource = noImage; //ImageSource.FromStream(() => new MemoryStream(thumbBytes));
                string fkey = pageDocs[q].Id.ToString();
                if (thumbsDict.ContainsKey(fkey))
                {
                    thumbSource= ImageSource.FromStream(() => new MemoryStream(thumbsDict[fkey].ToArray()));
                }

                var nII = new ImageItem
                {
                    FullImage = imageSource,
                    Thumbnail=thumbSource,
                    ImagePath= pageDocs[q].filePath,
                    Metadata = metaFinal,
                    PreviousRoute = pRoute,
                    NextRoute = nRoute,
                    IndexInTable = q
                };
                fList.Add(nII);
            }
            /*var newPage = pageDocs
                .Select(doc => new ImageItem { 
                    ThumbnailPath=doc.thumbnailPath, 
                    FullPath = doc.filePath,
                    Metadata=doc.metadata
                    
                })
                .ToList();*/
            PagedImages = new ObservableCollection<ImageItem>(fList);
            OnPropertyChanged(nameof(PagedImages));
            /*PagedImages.Clear();
            foreach (var doc in pageDocs)
            {
                PagedImages.Add(new ImageItem { ThumbnailPath = doc.filePath });
            }*/

            OnPropertyChanged(nameof(PageDisplayText));
            OnPropertyChanged(nameof(CanPrevPage));
            OnPropertyChanged(nameof(CanNextPage));
            OnPropertyChanged(nameof(HasMultiplePages));
            
        }
    }

    public class ImageItem
    {
        public required ImageSource Thumbnail { get; set; }
        public required ImageSource FullImage { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string Metadata { get; set; } = string.Empty;
        public PathRoute PreviousRoute { get; set; } = PathRoute.None;
        public bool CanGoPrevious => PreviousRoute!=PathRoute.None;
        public PathRoute NextRoute { get; set;} = PathRoute.None;
        public bool CanGoNext => NextRoute!=PathRoute.None;
        public int IndexInTable { get; set; } = -1;
    }
    public enum PathRoute
    {
        None, Image, Page
    }
}
