# JustStorytellers

A retexture of our beloved storytellers, using AI and some retouch here and there.
On 1.7 there is also an alternative version of each one.
In the mod settings you can change the portrait of any storyteller, even ones from other mods. You can pick this art, the portrait of another storyteller or your own images.
For your own images, put PNG or JPG files in the JustStorytellers folder inside the RimWorld save folder, there is a button to open it. 580x620 fits best.

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
