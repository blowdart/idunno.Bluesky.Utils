# bloop

Bloop is a command line utility to allow you to post to Bluesky.

## Usage

```powershell
bloop.exe "I'm posting from the command line" --handle:<YourHandle> --appPassword:<YourAppPassword> --saveLogin
```

If you use the --saveLogin option then you can continue using bloop without specifying the handle or app password.

```powershell
bloop.exe "Look ma no credentials!"
```

You can store credentials for multiple handles, but if you do that then you will always need to specify the handle with the --handle argument.

Saved credentials are stored in the [Windows Credential Store](https://support.microsoft.com/en-us/windows/credential-manager-in-windows-1b5c916a-6a16-889f-8581-fc16e8165ac0).

You can delete stored credentials with

```powershell
bloop.exe removelogin
```
