# JustStorytellers

A retexture of the storytellers, using AI and some retouch here and there.
On 1.7 there is also an alternative version of each one.
Now you are able to replace any storyteller image, original or from other mods. It lets you select from images from other mods or from a folder so you can put your own images.
For your own images, 580x620 fits best, and it lets you choose the small face version of the image as well by moving the square and using the scroll to zoom in and out.
You can also hide my 6 alt versions with a checkbox if you want.

Steam version: https://steamcommunity.com/sharedfiles/filedetails/?id=3217699484

## Versions

1.7 uses the Common and 1.7 folders, with the settings built from the code in the Source folder.

1.4 to 1.6 use the Legacy folder, it just replaces the vanilla portraits without options.

## Building

You need the .NET 10 SDK, then inside this folder run:

    dotnet build Source -c Release

The dll goes to 1.7/Assemblies. To test it, copy or link this folder to RimWorld/Mods.

## License

CC0, you can do whatever you want with it, no need to give credits.
