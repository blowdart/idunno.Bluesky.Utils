using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Meziantou.Framework.Win32;

using idunno.AtProto;
using idunno.Bluesky.Feed.Gates;

namespace idunno.Bluesky.Bloop
{
    internal sealed class Program
    {
        internal static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var statusArgument = new Argument<string>("status")
            {
                Description = "What you are doing right now?"
            };

            var handleOption = new Option<string?>("--handle", "-l", "/l")
            {
                Description = "The handle to post the status update under."
            };

            var appPasswordOption = new Option<string?>("--appPassword", "-p", "/p")
            {
                Description = "The app password to authenticate with."
            };

            var saveAppPasswordOption = new Option<bool>("--saveLogin", "-s", "/s")
            {
                Description = "Save the handle and app password.",
                DefaultValueFactory = defaultValue => false
            };

            var allowRepliesOption = new Option<bool>("--allowReplies", "-r", "/r")
            {
                Description = "Allow replies to your status update.",
                DefaultValueFactory = defaultValue => true
            };

            var rootCommand = new RootCommand("Post a quick status update on Bluesky")
            {
                statusArgument,
                allowRepliesOption,
                handleOption,
                appPasswordOption,
                saveAppPasswordOption
            };

            for (int i = 0; i < rootCommand.Options.Count; i++)
            {
                if (rootCommand.Options[i] is HelpOption defaultHelpOption)
                {
                    defaultHelpOption.Action = new CustomHelpAction((HelpAction)defaultHelpOption.Action!);
                    break;
                }
            }

            var removeLoginHandleOption = new Option<string?>("--handle", "-l", "/l")
            {
                Description = "The handle to remove the saved login information for. If not specified all saved credentials will be deleted."
            };

            var clearLogin = new Command("clear", "Clears any saved login information")
            {
                removeLoginHandleOption
            };
            rootCommand.Add(clearLogin);

            rootCommand.SetAction((parseResult, cancellationToken) =>
            {
                if (!string.IsNullOrWhiteSpace(parseResult.GetValue(statusArgument)))
                {
                    return Bloop(
                        status: parseResult.GetValue(statusArgument)!,
                        allowReplies: parseResult.GetValue(allowRepliesOption),
                        handle: parseResult.GetValue(handleOption),
                        appPassword: parseResult.GetValue(appPasswordOption),
                        saveAppPassword: parseResult.GetValue(saveAppPasswordOption),
                        cancellationToken: cancellationToken);
                }

                return Task.CompletedTask;
            });

            clearLogin.SetAction((parseResult, cancellationToken) =>
            {
                return RemoveLogin(
                    handle: parseResult.GetValue(removeLoginHandleOption),
                    cancellationToken);
            });


            ParseResult parseResult = rootCommand.Parse(args);

            if (!parseResult.Errors.Any())
            {
                return await parseResult.InvokeAsync().ConfigureAwait(false);
            }
            else
            {
                foreach (ParseError error in parseResult.Errors)
                {
                    ShowError(error.Message);
                }

                return -1;
            }
        }

        private static async Task<int> Bloop(
            string status,
            string? handle,
            string? appPassword,
            bool saveAppPassword = false,
            bool allowReplies = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(appPassword) && string.IsNullOrEmpty(handle))
            {
                if (!TryLoadCredentials(handle, out handle, out appPassword))
                {
                    ShowError("Credentials not specified, and no credentials were previously saved.");
                    return -2;
                }
            }
            else if (string.IsNullOrEmpty(appPassword))
            {
                if (!TryLoadCredentials(handle, out handle, out appPassword))
                {
                    ShowError($"Could not load saved credentials for {handle}");
                    return -3;
                }
            }
            else if (!string.IsNullOrEmpty(appPassword) && string.IsNullOrEmpty(handle))
            {               
                ShowError($"App password was specified but handle was not.");
                return -4;
            }

            using (var agent = new BlueskyAgent(
                options: new BlueskyAgentOptions()
                {
                    HttpClientOptions = new HttpClientOptions()
                    {
                        HttpUserAgent = "bloop/1.0"
                    }
                }))
            {
                AtProtoHttpResult<bool> loginResult = await agent.Login(handle: handle!, password: appPassword!, cancellationToken: cancellationToken).ConfigureAwait(true);
                if (loginResult.Succeeded && !cancellationToken.IsCancellationRequested)
                {
                    if (saveAppPassword)
                    {
                        SaveCredentials(handle!, appPassword!);
                    }

                    List<ThreadGateRule>? threadGates = null;

                    if (!allowReplies)
                    {
                        threadGates = [];
                    }

                    AtProtoHttpResult<AtProto.Repo.CreateRecordResult> postResult = await agent.Post(status, threadGateRules: threadGates, cancellationToken: cancellationToken).ConfigureAwait(true);

                    if (postResult.Succeeded)
                    {
                        Console.WriteLine($"✨ Blooped \"{status}\"");
                    }

                    await agent.Logout(cancellationToken: cancellationToken).ConfigureAwait(true);

                    return 0;
                }
                else
                {
                    ShowError($"Could not login {loginResult.StatusCode}");
                    if (loginResult.AtErrorDetail is not null)
                    {
                        ShowError($"\t{loginResult.AtErrorDetail.Error}");
                        if (!string.IsNullOrEmpty(loginResult.AtErrorDetail.Message))
                        {
                            ShowError($"\t{loginResult.AtErrorDetail.Message}");
                        }
                    }

                    return -5;
                }
            }
        }

        private const string ResourceName = "idunno.Bluesky.Utils.Bloop:handle=";

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "As we can't mix and match async and sync in System.CommandLine we must use an async signature.")]
        static async Task<int> RemoveLogin(string? handle, CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (string.IsNullOrEmpty(handle))
            {
                IReadOnlyList<Credential> credentialList = CredentialManager.EnumerateCredentials(ResourceName + "*");
                foreach (Credential credential in credentialList)
                {
                    CredentialManager.DeleteCredential(credential.ApplicationName);
                }
            }
            else
            {
                CredentialManager.DeleteCredential(ResourceName + handle);
            }

            return 0;
        }

        static void SaveCredentials(string handle, string appPassword)
        {
            CredentialManager.WriteCredential(
                applicationName: ResourceName + handle,
                userName: handle,
                secret: appPassword,
                persistence: CredentialPersistence.LocalMachine);
        }

        static bool TryLoadCredentials(string? handleFilter, out string? handle, out string? appPassword)
        {
            IReadOnlyList<Credential> credentialList = CredentialManager.EnumerateCredentials(ResourceName+"*");

            if (credentialList.Count > 0)
            {
                if (credentialList.Count == 1)
                {
                    handle = credentialList[0].UserName;
                    appPassword = credentialList[0].Password;

                    return true;
                }
                else
                {
                    foreach (Credential credential in credentialList)
                    {
                        if (credential.UserName == handleFilter)
                        {
                            handle = credentialList[0].UserName;
                            appPassword = credentialList[0].Password;

                            return true;
                        }
                    }

                    // Couldn't find one for the username

                    handle = handleFilter;
                    appPassword = null;

                    return true;
                }
            }

            handle = null;
            appPassword = null;
            return false;
        }

        static void ShowError(string message)
        {
            ConsoleColor currentColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write(message);
            Console.ForegroundColor = currentColor;
        }
    }


    internal sealed class CustomHelpAction(HelpAction action) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            int result = action.Invoke(parseResult);

            Console.WriteLine("If credentials have been previously saved with --saveLogin it is not necessary to provide them again.");
            Console.WriteLine("To delete saved credentials use 'bloop clear' to clear all saved credentials, or use 'boop clear --handle <handle>' to delete an specific credential.");

            return result;
        }
    }
}
