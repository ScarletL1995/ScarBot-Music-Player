using System.Collections.Concurrent;
using System.Threading.Channels;
using Concentus.Enums;
using Concentus.Structs;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using NAudio.Wave;
using ScarBot;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

public class MusicPlugin : IScarBotPlugin
{
    public string Name => "Music Plugin";

    [PluginService] IDatabaseManager DatabaseManager { get; set; } = null!;
    [PluginService] DiscordSocketClient Client { get; set; } = null!;
    [PluginService] ILogger Logger { get; set; } = null!;

    private static readonly ConcurrentDictionary<ulong, GuildMusicState> musicStates = new ConcurrentDictionary<ulong, GuildMusicState>();

    public void Initialize()
    {
        YoutubeClient _ = new YoutubeClient();
    }

    public void RegisterCommands(ICommandRegistrar handler)
    {
        // Standard Music Commands
        handler.RegisterCommand(new Command
        {
            Name = "join",
            ShortDescription = "Bot joins your voice channel",
            LongDescription = "Instructs the bot to join your current voice channel.",
            Category = "Music",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await JoinVoiceChannel(ctx),
            Parameters = new List<CommandOption>()
        });

        handler.RegisterCommand(new Command
        {
            Name = "leave",
            ShortDescription = "Bot leaves the voice channel",
            LongDescription = "Instructs the bot to leave the current voice channel.",
            Category = "Music",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await LeaveVoiceChannel(ctx),
            Parameters = new List<CommandOption>()
        });

        handler.RegisterCommand(new Command
        {
            Name = "play",
            ShortDescription = "Play a song",
            LongDescription = "Plays a song from a given URL or search term.",
            Category = "Music",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await PlaySong(ctx),
            Parameters = new List<CommandOption>
        {
            new CommandOption
            {
                Name = "query",
                Description = "The song URL or search term",
                Type = ApplicationCommandOptionType.String,
                Required = true
            }
        }
        });

        handler.RegisterCommand(new Command
        {
            Name = "pause",
            ShortDescription = "Pause the current song",
            LongDescription = "Pauses the currently playing song.",
            Category = "Music",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await PauseSong(ctx),
            Parameters = new List<CommandOption>()
        });

        handler.RegisterCommand(new Command
        {
            Name = "resume",
            ShortDescription = "Resume the paused song",
            LongDescription = "Resumes the currently paused song.",
            Category = "Music",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await ResumeSong(ctx),
            Parameters = new List<CommandOption>()
        });

        handler.RegisterCommand(new Command
        {
            Name = "skip",
            ShortDescription = "Skip the current song",
            LongDescription = "Skips the currently playing song.",
            Category = "Music",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await SkipSong(ctx),
            Parameters = new List<CommandOption>()
        });

        handler.RegisterCommand(new Command
        {
            Name = "queue",
            ShortDescription = "Show the song queue",
            LongDescription = "Displays the current song queue.",
            Category = "Music",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await ShowQueue(ctx),
            Parameters = new List<CommandOption>()
        });

        handler.RegisterCommand(new Command
        {
            Name = "clear",
            ShortDescription = "Clear the song queue",
            LongDescription = "Clears all songs from the queue.",
            Category = "Music",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await ClearQueue(ctx),
            Parameters = new List<CommandOption>()
        });

        handler.RegisterCommand(new Command
        {
            Name = "nowplaying",
            ShortDescription = "Show the currently playing song",
            LongDescription = "Displays information about the currently playing song.",
            Category = "Music",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await ShowNowPlaying(ctx),
            Parameters = new List<CommandOption>()
        });

        // Playlist Commands
        handler.RegisterCommand(new Command
        {
            Name = "pl-create",
            ShortDescription = "Create a new playlist",
            LongDescription = "Creates a new named playlist for your account.",
            Category = "Playlists",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await CreatePlaylist(ctx),
            Parameters = new List<CommandOption>
        {
            new CommandOption
            {
                Name = "name",
                Description = "Playlist name",
                Type = ApplicationCommandOptionType.String,
                Required = true
            }
        }
        });

        handler.RegisterCommand(new Command
        {
            Name = "pl-rename",
            ShortDescription = "Rename a playlist",
            LongDescription = "Renames one of your existing playlists.",
            Category = "Playlists",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await RenamePlaylist(ctx),
            Parameters = new List<CommandOption>
        {
            new CommandOption
            {
                Name = "old",
                Description = "Old playlist name",
                Type = ApplicationCommandOptionType.String,
                Required = true
            },
            new CommandOption
            {
                Name = "new",
                Description = "New playlist name",
                Type = ApplicationCommandOptionType.String,
                Required = true
            }
        }
        });

        handler.RegisterCommand(new Command
        {
            Name = "pl-add",
            ShortDescription = "Add song to playlist",
            LongDescription = "Adds a YouTube URL to the specified playlist.",
            Category = "Playlists",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await AddSongToPlaylist(ctx),
            Parameters = new List<CommandOption>
        {
            new CommandOption
            {
                Name = "playlist",
                Description = "Playlist name",
                Type = ApplicationCommandOptionType.String,
                Required = true
            },
            new CommandOption
            {
                Name = "url",
                Description = "YouTube URL",
                Type = ApplicationCommandOptionType.String,
                Required = true
            }
        }
        });

        handler.RegisterCommand(new Command
        {
            Name = "pl-remove",
            ShortDescription = "Remove song from playlist",
            LongDescription = "Removes a YouTube URL from the specified playlist.",
            Category = "Playlists",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await RemoveSongFromPlaylist(ctx),
            Parameters = new List<CommandOption>
        {
            new CommandOption
            {
                Name = "playlist",
                Description = "Playlist name",
                Type = ApplicationCommandOptionType.String,
                Required = true
            },
            new CommandOption
            {
                Name = "url",
                Description = "YouTube URL",
                Type = ApplicationCommandOptionType.String,
                Required = true
            }
        }
        });

        handler.RegisterCommand(new Command
        {
            Name = "pl-share",
            ShortDescription = "Share playlist",
            LongDescription = "Allows another user to access your playlist.",
            Category = "Playlists",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await SharePlaylist(ctx),
            Parameters = new List<CommandOption>
        {
            new CommandOption
            {
                Name = "playlist",
                Description = "Playlist name",
                Type = ApplicationCommandOptionType.String,
                Required = true
            },
            new CommandOption
            {
                Name = "user",
                Description = "User ID to share with",
                Type = ApplicationCommandOptionType.String,
                Required = true
            }
        }
        });

        handler.RegisterCommand(new Command
        {
            Name = "pl-play",
            ShortDescription = "Play songs from a playlist",
            LongDescription = "Loads a playlist and queues the songs for playback.",
            Category = "Playlists",
            Aliases = new List<string>(),
            Function = async (CommandContext ctx) => await PlayPlaylist(ctx),
            Parameters = new List<CommandOption>
        {
            new CommandOption
            {
                Name = "playlist",
                Description = "Playlist name",
                Type = ApplicationCommandOptionType.String,
                Required = true
            }
        }
        });

        Logger.LogAsync($"Registered music and playlist commands for plugin {Name}", LOGGER.INFO);
    }

    private class SongInfo
    {
        public required string Url { get; set; }
        public string Title { get; set; } = "";
        public string RequestedBy { get; set; } = "";
    }

    private readonly YoutubeClient youtube = new();

    private async Task JoinVoiceChannel(CommandContext ctx)
    {
        try
        {
            if (ctx.SlashCommand?.GuildId is null || ctx.User is null)
            {
                await ctx.RespondError("This command must be used in a server.");
                return;
            }

            if (!ctx.IsInVoiceChannel())
            {
                await ctx.RespondError("You must be in a voice channel first.");
                return;
            }

            SocketVoiceChannel? voiceChannel = ctx.GetVoiceChannel();
            if (voiceChannel == null)
            {
                await ctx.RespondError("Could not find your voice channel.");
                return;
            }

            IAudioClient audioClient = await voiceChannel.ConnectAsync();
            GuildMusicState state = new GuildMusicState(audioClient);
            musicStates[voiceChannel.Guild.Id] = state;

            _ = Task.Run(() => ProcessQueue(ctx.GetGuildID()));

            await ctx.RespondInfo($"Joined {voiceChannel.Name}.");
        }
        catch
        {
            await ctx.RespondError("Failed to join the voice channel. Please ensure I have permission to connect and speak in that channel.");
        }
    }


    private async Task LeaveVoiceChannel(CommandContext ctx)
    {
        if (!musicStates.TryRemove(ctx.GetGuildID(), out var state))
        {
            await ctx.RespondError("Not connected to a voice channel.");
            return;
        }

        await state.Client.StopAsync();
        await ctx.RespondInfo("Left the voice channel.");
    }

    private async Task PlaySong(CommandContext ctx)
    {
        ulong guildId = ctx.GetGuildID();

        if (!musicStates.TryGetValue(guildId, out GuildMusicState? state))
        {
            if (!ctx.IsInVoiceChannel())
            {
                await ctx.RespondError("You must be in a voice channel to start playing music.");
                return;
            }

            SocketVoiceChannel? voiceChannel = ctx.GetVoiceChannel();
            if (voiceChannel == null)
            {
                await ctx.RespondError("Failed to get your voice channel.");
                return;
            }

            IAudioClient audioClient = await voiceChannel.ConnectAsync();
            state = new GuildMusicState(audioClient);
            musicStates[guildId] = state;
            _ = Task.Run(() => ProcessQueue(guildId));
            await ctx.RespondInfo($"Joined {voiceChannel.Name}.");
        }

        string? query = ctx.SlashCommand?.Data.Options.FirstOrDefault(o => o.Name == "query")?.Value as string;
        if (string.IsNullOrWhiteSpace(query))
        {
            await ctx.RespondError("You must provide a search query or URL.");
            return;
        }

        YoutubeClient youtube = new YoutubeClient();
        Video video;
        try
        {
            video = await youtube.Videos.GetAsync(query);
        }
        catch (Exception)
        {
            await ctx.RespondError("No results found for that query.");
            return;
        }

        SongInfo song = new SongInfo
        {
            Url = video.Url,
            Title = video.Title,
            RequestedBy = ctx.GetUserDisplayName()
        };

        await state.Queue.Writer.WriteAsync(System.Text.Json.JsonSerializer.Serialize(song));
        await ctx.RespondInfo($"Added to queue: **{video.Title}**");
    }


    private async Task PauseSong(CommandContext ctx)
    {
        if (musicStates.TryGetValue(ctx.GetGuildID(), out GuildMusicState? state))
        {
            state.Paused = true;
            await ctx.RespondInfo("Playback paused.");
        }
        else
        {
            await ctx.RespondInfo("Nothing is playing.");
        }
    }

    private async Task ResumeSong(CommandContext ctx)
    {
        if (musicStates.TryGetValue(ctx.GetGuildID(), out GuildMusicState? state))
        {
            state.Paused = false;
            await ctx.RespondInfo("Resuming playback.");
        }
        else
        {
            await ctx.RespondInfo("Nothing to resume.");
        }
    }

    private async Task SkipSong(CommandContext ctx)
    {
        if (musicStates.TryGetValue(ctx.GetGuildID(), out GuildMusicState? state))
        {
            state.SkipRequested = true;
            await ctx.RespondInfo("Skipping current song.");
        }
        else
        {
            await ctx.RespondInfo("Nothing to skip.");
        }
    }

    private async Task ShowQueue(CommandContext ctx)
    {
        if (!musicStates.TryGetValue(ctx.GetGuildID(), out var state))
        {
            await ctx.RespondInfo("No queue available.");
            return;
        }

        var queuedItems = state.Queue.Reader.Count;
        await ctx.RespondInfo($"There are **{queuedItems}** song(s) in the queue.");
    }

    private async Task ClearQueue(CommandContext ctx)
    {
        if (musicStates.TryGetValue(ctx.GetGuildID(), out var state))
        {
            while (state.Queue.Reader.TryRead(out _)) ;
            await ctx.RespondInfo("Queue cleared.");
        }
        else
        {
            await ctx.RespondInfo("No queue to clear.");
        }
    }

    private async Task ShowNowPlaying(CommandContext ctx)
    {
        if (!musicStates.TryGetValue(ctx.GetGuildID(), out var state) || state.CurrentSong == null)
        {
            await ctx.RespondInfo("Nothing is currently playing.");
            return;
        }

        var current = state.CurrentSong;
        await ctx.RespondInfo($"🎶 Now Playing: **{current.Title}** (Requested by {current.RequestedBy})");
    }

    private async Task<(string Url, string Title)> GetVideoInfo(string query)
    {
        if (YoutubeClient.TryParseVideoId(query, out var id))
        {
            var video = await youtube.Videos.GetAsync(id);
            return (video.Url, video.Title);
        }

        var search = await youtube.Search.GetVideosAsync(query).CollectAsync(1);
        var first = search.First();
        return (first.Url, first.Title);
    }

    private async Task<(Stream Stream, IStreamInfo Info)> GetAudioStream(string url)
    {
        var videoId = YoutubeClient.ParseVideoId(url);
        var manifest = await youtube.Videos.Streams.GetManifestAsync(videoId);
        var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        var stream = await youtube.Videos.Streams.GetAsync(streamInfo);
        return (stream, streamInfo);
    }

    private Stream ConvertToPcm(Stream input)
    {
        var mp3Reader = new MediaFoundationReader(input);
        var resampler = new MediaFoundationResampler(mp3Reader, new WaveFormat(48000, 16, 2));
        resampler.ResamplerQuality = 60;
        return resampler;
    }

    private async Task CreatePlaylist(CommandContext ctx)
    {
        string name = ctx.GetArgument<string>("name");
        if (!await DatabaseManager.IsUserLoggedIn(ctx.User.Id.ToString()))
        {
            await ctx.RespondError("You must be logged in.");
            return;
        }

        await DatabaseManager.CreateUserTable(ctx.User.Id.ToString(), $"playlist_{name}");
        await ctx.RespondInfo($"Playlist '{name}' created.");
    }

    private async Task RenamePlaylist(CommandContext ctx)
    {
        string oldName = ctx.GetArgument<string>("old");
        string newName = ctx.GetArgument<string>("new");

        if (!await DatabaseManager.IsUserLoggedIn(ctx.User.Id.ToString()))
        {
            await ctx.RespondError("You must be logged in.");
            return;
        }

        await DatabaseManager.RenameUserTable(ctx.User.Id.ToString(), $"playlist_{oldName}", $"playlist_{newName}");
        await ctx.RespondInfo($"Renamed playlist to '{newName}'.");
    }

    private async Task AddSongToPlaylist(CommandContext ctx)
    {
        string playlist = ctx.GetArgument<string>("playlist");
        string url = ctx.GetArgument<string>("url");

        if (!await DatabaseManager.IsUserLoggedIn(ctx.User.Id.ToString()))
        {
            await ctx.RespondError("You must be logged in.");
            return;
        }

        await DatabaseManager.InsertUserData(ctx.User.Id.ToString(), $"playlist_{playlist}", new() { { "url", url } });
        await ctx.RespondInfo("Added song to playlist.");
    }

    private async Task RemoveSongFromPlaylist(CommandContext ctx)
    {
        string playlist = ctx.GetArgument<string>("playlist");
        string url = ctx.GetArgument<string>("url");

        if (!await DatabaseManager.IsUserLoggedIn(ctx.User.Id.ToString()))
        {
            await ctx.RespondError("You must be logged in.");
            return;
        }

        await DatabaseManager.DeleteUserData(ctx.User.Id.ToString(), $"playlist_{playlist}", "url", url);
        await ctx.RespondInfo("Removed song from playlist.");
    }

    private async Task SharePlaylist(CommandContext ctx)
    {
        string playlist = ctx.GetArgument<string>("playlist");
        string target = ctx.GetArgument<string>("user");

        if (!await DatabaseManager.IsUserLoggedIn(ctx.User.Id.ToString()))
        {
            await ctx.RespondError("You must be logged in.");
            return;
        }

        await DatabaseManager.GrantUserTableAccess(ctx.User.Id.ToString(), target, $"playlist_{playlist}");
        await ctx.RespondInfo("Playlist shared.");
    }

    private async Task PlayPlaylist(CommandContext ctx)
    {
        string playlist = ctx.GetArgument<string>("playlist");

        if (!await DatabaseManager.IsUserLoggedIn(ctx.User.Id.ToString()))
        {
            await ctx.RespondError("You must be logged in.");
            return;
        }

        var songs = await DatabaseManager.GetUserTable(ctx.User.Id.ToString(), $"playlist_{playlist}");
        if (songs == null || songs.Count == 0)
        {
            await ctx.RespondError("That playlist is empty or inaccessible.");
            return;
        }

        if (!musicStates.TryGetValue(ctx.Guild.Id, out var state))
        {
            await ctx.RespondError("Bot is not in a voice channel.");
            return;
        }

        foreach (var row in songs)
        {
            if (row.TryGetValue("url", out var url) && url is string urlStr)
            {
                await state.Queue.Writer.WriteAsync(urlStr);
            }
        }

        await ctx.RespondInfo($"Queued {songs.Count} songs from '{playlist}'.");
    }

    private async Task ProcessQueue(ulong guildId)
    {
        var client = new YoutubeClient();

        if (!musicStates.TryGetValue(guildId, out var state)) return;

        while (await state.Queue.Reader.WaitToReadAsync())
        {
            while (state.Queue.Reader.TryRead(out var query))
            {
                try
                {
                    var video = await client.Videos.GetAsync(query);
                    var streamManifest = await client.Videos.Streams.GetManifestAsync(video.Id);
                    var streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                    if (streamInfo == null) continue;

                    using var httpClient = new HttpClient();
                    using var input = await httpClient.GetStreamAsync(streamInfo.Url);
                    using var mp3 = new MediaFoundationReader(input);
                    using var pcm = WaveFormatConversionStream.CreatePcmStream(mp3);

                    var buffer = new byte[3840];
                    var encoder = OpusEncoder.Create(48000, 2, OpusApplication.OPUS_APPLICATION_AUDIO);

                    var stream = state.Client.CreatePCMStream(AudioApplication.Mixed);
                    int read;
                    while ((read = pcm.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        while (state.Paused) await Task.Delay(500);
                        if (state.SkipRequested)
                        {
                            state.SkipRequested = false;
                            break;
                        }

                        await stream.WriteAsync(buffer, 0, read);
                    }

                    await stream.FlushAsync();
                }
                catch
                {
                    // Optionally log or notify
                }
            }
        }
    }
}

public class GuildMusicState
{
    public IAudioClient Client { get; }
    public Channel<string> Queue { get; } = Channel.CreateUnbounded<string>();
    public bool SkipRequested { get; set; }
    public bool Paused { get; set; }

    public GuildMusicState(IAudioClient client)
    {
        Client = client;
    }
}
