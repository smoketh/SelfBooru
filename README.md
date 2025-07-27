# SelfBooru
![alt text](Resources/AppIcon/selfbooru2.scale-400.png "SelfBooru Logo")

MAUI app which acts like a local booru for all your Stable Diffusion A1111/Forge images

After 2 years of SD generations I needed a way to search through all the images. 

So here's a solution for that. At the moment it's mainly a windows app, 
but with a bit of editing it can be compiled on mac. 

## Usage
> [!NOTE]  
> Click gear icon to the top-right, input your sd output dir 
>
> to example
>
>`D:\SD-Forge\webui\outputs\txt2image-images` 
>
> and click start scan. It will report when scan is complete.

As of right now A1111/SD-Forge metadata format is supported, 
stealth metadata is also supported. It uses comma separated tags for tags, 
strips weights, also adds loras/lycos and models with `lora:` `lyco:` `model:` prefix into the tags as well.

Can rescan detecting md5 hash changes, and will regenerate thumbnails when needed.

Under the hood it uses litedb to store database information, 
a separate file to store thumbnails in 100x100 80% jpg format, 
and also caches loaded images during runtime to reduce disk hits, 
with max cache size being set at 2gb at the moment.

For 20k images indexed - `thumbs.pack` is 64'ish megabytes. 


<p align="center">
  <img width="200" height="auto" as src="Screenshots/scr1.png"/>
  <img width="200" height="auto" as src="Screenshots/scr2.png"/>
  <img width="200" height="auto" as src="Screenshots/scr3.png"/>
  <img width="200" height="auto" as src="Screenshots/scr4.png"/>
</p>