# blueskyVideoDownloader

BlueskyVideoDownloader is a command line utility to allow you to download videos from Bluesky, and any associated caption files.

Download the latest release from the [Releases](https://github.com/blowdart/idunno.Bluesky.Utils/releases) page.

## Usage

```powershell
.\BlueskyVideoDownloader.exe https://bsky.app/profile/blowdart.me/post/3l5q7ujtvde23
```

To get the link for the post to download from, click the share button on the post in the Bluesky site or app,
and then select "Copy link to post".

Videos will downloaded to the current directory. If you want to specify a different directory and/or filename, use the `--output` argument.

```powershell
.\BlueskyVideoDownloader.exe https://bsky.app/profile/blowdart.me/post/3l5q7ujtvde23 --output:C:\Videos\myvideo.mp4
```

If you do not specify a filename and the generated filename already exists, or if you specify a filename with `--output` that already exists
use `--force` to overwrite the existing file.

```powershell
.\BlueskyVideoDownloader.exe https://bsky.app/profile/blowdart.me/post/3l5q7ujtvde23 --output:C:\Videos\myvideo.mp4 --force
```
