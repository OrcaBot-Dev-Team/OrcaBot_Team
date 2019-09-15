using BotCoreNET;
using BotCoreNET.BotVars;
using BotCoreNET.Helpers;
using Discord;
using Discord.WebSocket;
using JSON;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OrcaBot
{
    class Quote : IGenericBotVar
    {
        internal int QuoteId;
        internal ulong GuildId;
        internal string ChannelName;
        internal ulong ChannelId;
        internal ulong MessageId;
        internal string MessageContent;
        internal string ImageURL;
        internal ulong AuthorId;
        internal string AuthorName;
        internal DateTimeOffset Timestamp;
        public Quote()
        {

        }

        public bool ApplyJSON(JSONContainer json)
        {
            if (json.TryGetField(JSON_ID, out QuoteId) && json.TryGetField(JSON_CONTENT, out MessageContent) && json.TryGetField(JSON_TIMESTAMP, out string timestamp_str) && json.TryGetField(JSON_GUILD_ID, out GuildId))
            {
                json.TryGetField(JSON_AUTHOR_ID, out AuthorId);
                json.TryGetField(JSON_AUTHOR_NAME, out AuthorName, "Unknown Author");
                json.TryGetField(JSON_CHANNEL_NAME, out ChannelName, "Unknown Channel");
                json.TryGetField(JSON_MESSAGE_ID, out MessageId);
                json.TryGetField(JSON_CHANNEL_ID, out ChannelId);
                json.TryGetField(JSON_IMAGE_URL, out ImageURL, string.Empty);
                return DateTimeOffset.TryParseExact(timestamp_str, "u", CultureInfo.InvariantCulture, DateTimeStyles.None, out Timestamp);
            }
            return false;
        }

        public JSONContainer ToJSON()
        {
            JSONContainer result = JSONContainer.NewObject();
            result.TryAddField(JSON_ID, QuoteId);
            result.TryAddField(JSON_MESSAGE_ID, MessageId);
            result.TryAddField(JSON_CHANNEL_NAME, ChannelName);
            result.TryAddField(JSON_CONTENT, MessageContent);
            result.TryAddField(JSON_IMAGE_URL, ImageURL);
            result.TryAddField(JSON_AUTHOR_ID, AuthorId);
            result.TryAddField(JSON_AUTHOR_NAME, AuthorName);
            result.TryAddField(JSON_TIMESTAMP, Timestamp.ToString("u"));
            result.TryAddField(JSON_CHANNEL_ID, ChannelId);
            result.TryAddField(JSON_GUILD_ID, GuildId);
            return result;
        }

        public Quote(ulong guildId, IMessage msg)
        {
            StoredMessagesService guildMessageService = StoredMessagesService.GetMessagesService(guildId);

            List<IAttachment> attachments = new List<IAttachment>(msg.Attachments);

            QuoteId = 0;
            GuildId = guildId;
            ChannelName = msg.Channel.Name;
            ChannelId = msg.Channel.Id;
            MessageId = msg.Id;
            MessageContent = msg.Content;
            AuthorId = msg.Author.Id;
            AuthorName = msg.Author.ToString();
            Timestamp = msg.Timestamp;
            if (attachments.Count > 0)
            {
                ImageURL = attachments[0].Url;
            }
            else
            {
                string attachment = null;
                foreach (string word in msg.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    word.Trim();
                    if (word.IsValidImageURL())
                    {
                        attachment = word;
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(attachment))
                {
                    ImageURL = attachment;
                }
            }
        }

        internal EmbedBuilder GetEmbed()
        {
            EmbedBuilder quote = new EmbedBuilder
            {
                Color = BotCore.EmbedColor,
                Description = $"**Message Link**\n({Markdown.MessageURL(GuildId, ChannelId, MessageId)})\n\n**Message**\n{MessageContent}"
            };
            EmbedAuthorBuilder authorBuilder = new EmbedAuthorBuilder();

            SocketGuild guild = BotCore.Client.GetGuild(GuildId);
            SocketGuildUser author = null;
            if (guild != null)
            {
                author = guild.GetUser(AuthorId);
            }
            if (author != null)
            {
                if (!string.IsNullOrEmpty(author.Nickname))
                {
                    authorBuilder.Name = author.Nickname;
                }
                else
                {
                    authorBuilder.Name = author.Username;
                }
                authorBuilder.IconUrl = author.GetAvatarUrl();
            }
            else
            {
                authorBuilder.Name = AuthorName;
                authorBuilder.IconUrl = "https://cdn.discordapp.com/embed/avatars/0.png";
            }
            authorBuilder.Url = Markdown.MessageURL(GuildId, ChannelId, MessageId);

            quote.Author = authorBuilder;
            if (!string.IsNullOrEmpty(ImageURL))
            {
                quote.ImageUrl = ImageURL;
            }
            EmbedFooterBuilder footer = new EmbedFooterBuilder
            {
                Text = string.Format("#{0}, QuoteId: {1}", ChannelName, QuoteId)
            };

            quote.Footer = footer;
            quote.Timestamp = Timestamp;

            return quote;
        }

        private const string JSON_ID = "Id";
        private const string JSON_CHANNEL_NAME = "ChannelName";
        private const string JSON_CHANNEL_ID = "ChannelId";
        private const string JSON_GUILD_ID = "GuildId";
        private const string JSON_MESSAGE_ID = "MessageId";
        private const string JSON_CONTENT = "Content";
        private const string JSON_IMAGE_URL = "ImageURL";
        private const string JSON_AUTHOR_ID = "AuthorId";
        private const string JSON_AUTHOR_NAME = "AuthorName";
        private const string JSON_TIMESTAMP = "TimeStamp";

        public static implicit operator EmbedBuilder(Quote q)
        {
            return q.GetEmbed();
        }
    }
}
