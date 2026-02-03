// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Text;

using Spinner;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.VideoDownloader
{
    internal sealed class Program
    {
        const string PostPrefix = "https://bsky.app/profile/";

        internal static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // As there is no default value, this argument is required.
            var linkArgument = new Argument<string>("link")
            {
                Description = "The link to the post whose video should be downloaded.",
            };

            linkArgument.Validators.Add(result =>
            {
                if (string.IsNullOrEmpty(result.GetValue(linkArgument)))
                {
                    result.AddError("Must not be empty.");
                    return;
                }

                if (!result.GetValue(linkArgument)!.StartsWith(PostPrefix))
                {
                    result.AddError($"Invalid link. The link must begin with {PostPrefix}");
                    return;
                }

                if (!Uri.TryCreate(result.GetValue(linkArgument)!, new UriCreationOptions(), out Uri? parsedUri) ||
                    parsedUri is null)
                {
                    result.AddError($"Cannot build a URI from {result.GetValue(linkArgument)}.");
                    return;
                }
            });

            var filePathOption = new Option<string?>("--output", "-o", "/o")
            {
                Description = "The file or directory to save the downloaded video to. If not specified, the video will be saved to the current directory with a name generated from the Post URI."
            };

            filePathOption.Validators.Add(result =>
            {
                string? filePath = result.GetValue(filePathOption);

                if (!string.IsNullOrEmpty(filePath))
                {
                    // Validate directory exists
                    if (filePath.EndsWith(Path.DirectorySeparatorChar) || filePath.EndsWith(Path.AltDirectorySeparatorChar) && !Directory.Exists(filePath))
                    {
                        result.AddError($"{filePath} does not exist.");
                        return;
                    }
                    else
                    {
                        string? directory = Path.GetDirectoryName(filePath);
                        if (directory is not null && !Directory.Exists(directory))
                        {
                            result.AddError($"{directory} does not exist.");
                            return;
                        }
                    }
                }
            });

            // As this is a bool option, the default is false, but if the argument is present without a value it will evaluate as true.
            var forceOption = new Option<bool>("--force", "--overwrite")
            {
                Description = "Overwrite the specified file name, if any."
            };

            forceOption.Validators.Add(result =>
            {
                string? filePath = result.GetValue(filePathOption);
                bool force = result.GetValue(forceOption);

                if (!force && !string.IsNullOrEmpty(filePath))
                {
                    if (!filePath.EndsWith(Path.DirectorySeparatorChar) &&
                        !filePath.EndsWith(Path.AltDirectorySeparatorChar) &&
                        File.Exists(filePath))
                    {
                        result.AddError($"{filePath} exists. Use --force to overwrite it.");
                        return;
                    }
                }
            });

            var rootCommand = new RootCommand("Download a video from a Bluesky post.")
            {
                linkArgument,
                filePathOption,
                forceOption
            };

            rootCommand.SetAction((parseResult, cancellationToken) =>
            {
                if (linkArgument is not null)
                {
                    return DownloadVideo(
                        uri: parseResult.GetValue(linkArgument)!,
                        filePath: parseResult.GetValue(filePathOption),
                        force: parseResult.GetValue(forceOption),
                        cancellationToken: cancellationToken);
                }

                return Task.CompletedTask;
            });

            ParseResult parseResult = rootCommand.Parse(args);

            return await parseResult.InvokeAsync().ConfigureAwait(false);
        }

        private static async Task<int> DownloadVideo(
            string uri,
            string? filePath,
            bool force,
            CancellationToken cancellationToken = default)
        {
            using (var agent = new BlueskyAgent(
                options: new BlueskyAgentOptions()
                {
                    HttpClientOptions = new HttpClientOptions()
                    {
                        HttpUserAgent = "idunno.Bluesky.VideoDownloader/1.0"
                    }
                }))
            {
                uri = uri.Replace(PostPrefix, null);
                string[] uriParts = uri.Split('/');

                Handle handle = uriParts[0];

                Did? did = await agent.ResolveHandle(handle, cancellationToken).ConfigureAwait(false);

                if (did is null)
                {
                    Console.WriteLine($"Could not resolve {handle} to DID.");
                    return -1;
                }

                Uri? pds = await agent.ResolvePds(did, cancellationToken).ConfigureAwait(false);

                if (pds is null)
                {
                    Console.WriteLine($"Could not discover PDS for {handle}.");
                    return -2;
                }

                if (!string.Equals(uriParts[1], "post", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("URI does not point to a Bluesky post.");
                    return -2;
                }

                if (!RecordKey.TryParse(uriParts[2], out RecordKey? recordKey))
                {
                    Console.WriteLine("RecordKey is not in the correct format.");
                    return -3;
                }

                AtUri postUri = $"at://{did}/{CollectionNsid.Post}/{recordKey}";
                AtProtoHttpResult<AtProtoRepositoryRecord<Post>> getPostRecordResult = await agent.GetPostRecord(postUri, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!getPostRecordResult.Succeeded)
                {
                    Console.WriteLine("Could not retrieve post.");
                    return -4;
                }

                AtProtoRepositoryRecord<Post> post = getPostRecordResult.Result;
                if (post.Value.EmbeddedRecord is null || post.Value.EmbeddedRecord is not EmbeddedVideo video)
                {
                    Console.WriteLine("Post does not contain an embedded video.");
                    return -5;
                }

                if (video.Video.Reference.Link is null)
                {
                    Console.WriteLine("Video URI could not be determined.");
                    return -6;
                }

                if (!Uri.TryCreate($"{pds}xrpc/com.atproto.sync.getBlob?did={did}&cid={video.Video.Reference.Link}", UriKind.Absolute, out Uri? blobUri))
                {
                    Console.WriteLine("Video URI could not be constructed.");
                    return -7;
                }

                Console.WriteLine(Path.GetFileName(filePath));

                // Fix up file path as needed
                if (filePath is null || filePath.Length == 0)
                {
                    filePath = $"{recordKey}.mp4";
                }
                else if (filePath.EndsWith(Path.DirectorySeparatorChar) || filePath.EndsWith(Path.AltDirectorySeparatorChar))
                {
                    filePath = Path.Combine(filePath, $"{recordKey}.mp4");
                }

                if (Directory.Exists(filePath))
                {
                    filePath = Path.Combine(filePath, $"{recordKey}.mp4");
                }

                if (!force && File.Exists(filePath))
                {
                    Console.WriteLine($"{filePath} exists. To overwrite use --force");
                    return -8;
                }

                Console.Write($"Downloading video ");

                using (_ = ConsoleEx.StartSpinner(Animations.Sparkle))
                using (var client = new HttpClient())
                {
                    using (Stream httpStream = await client.GetStreamAsync(blobUri, cancellationToken: cancellationToken).ConfigureAwait(false))
                    using (FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await httpStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"Video saved as {filePath}");

                if (video.Captions is not null && video.Captions.Count != 0)
                {
                    List<string> savedAs = [];

                    Console.Write($"Downloading captions ");

                    using (_ = ConsoleEx.StartSpinner(Animations.Sparkle))
                    using (var client = new HttpClient())
                    {
                        foreach (Caption caption in video.Captions)
                        {
                            if (!Uri.TryCreate($"{pds}xrpc/com.atproto.sync.getBlob?did={did}&cid={caption.File.Reference.Link}", UriKind.Absolute, out Uri? captionUri))
                            {
                                break;
                            }

                            string extension = "vtt";

                            if (video.Captions.Count > 1)
                            {
                                extension = $"{caption.Lang}.vtt";
                            }

                            string captionPath = Path.ChangeExtension(filePath, extension);

                            using (Stream httpStream = await client.GetStreamAsync(captionUri, cancellationToken: cancellationToken).ConfigureAwait(false))
                            using (FileStream fileStream = new(captionPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await httpStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                            }

                            savedAs.Add(captionPath);
                        }
                    }

                    Console.WriteLine();
                    foreach (string savedFile in savedAs)
                    {
                        Console.WriteLine($"Captions saved as {savedFile}");
                    }
                }

                return 0;
            }
        }
    }
}
