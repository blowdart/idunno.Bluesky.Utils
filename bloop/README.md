# bloop

Bloop is a command line utility to allow you to post to Bluesky.

Download the latest release from the [Releases](https://github.com/blowdart/idunno.Bluesky.Utils/releases) page.

## Usage

```powershell
.\bloop.exe "I'm posting from the command line" --handle:<YourHandle> --appPassword:<YourAppPassword> --saveLogin
```
Note that PowerShell saves your command history including arguments, so be careful when using the `--appPassword` argument.

If you want to save your credentials for later use, include the `--saveLogin` argument.

```powershell
.\bloop.exe "I'm posting from the command line" --handle:<YourHandle> --appPassword:<YourAppPassword> --saveLogin
```

Running

```powershell
Clear-History -Count 1 -newest
```

immediately after saving your credentials will remove the command, and your credentialsfrom your PowerShell history.

You can then continue using bloop without specifying the handle or app password.

```powershell
.\bloop.exe "Look ma no credentials!"
```

You can store credentials for multiple handles, but if you do that then you will always need to specify the handle with the --handle argument.

Saved credentials are stored in the [Windows Credential Store](https://support.microsoft.com/en-us/windows/credential-manager-in-windows-1b5c916a-6a16-889f-8581-fc16e8165ac0).

You can delete stored credentials with

```powershell
.\bloop.exe removelogin
```
