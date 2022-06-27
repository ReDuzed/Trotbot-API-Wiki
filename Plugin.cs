using System;
using System.Collections.Generic;
using twitchbot;
using twitchbot.api;

namespace Polls
{
    [ApiVersion(0, 3)]
    public class Plugin : TwitchBot
    {
        public override char CommandChar => '~';
        public override string Name => "Chat Polls";
        public override Version Version => new Version(1, 0, 0, 0);
        public Command 
            make,
            close,
            _winner,
            _current;
        public static ChatRoom Chat
        {
            get { return ChatRoom.Instance; }
        }
        public override void Dispose()
        {
            make    .OnCommandEvent  -= Make_OnCommandEvent;
            close   .OnCommandEvent  -= Close_OnCommandEvent;
            _winner .OnCommandEvent  -= Winner_OnCommandEvent;
            _current.OnCommandEvent  -= _current_OnCommandEvent;
            ChatRoom.AllCommandEvent -= ChatRoom_AllCommandEvent;
        }
        public override void Initialize()
        {
            var perm = new BadgeType[] { BadgeType.Broadcaster, BadgeType.Moderator };

            make     = ChatRoom.AddCommand(this, new Command(this, "makepoll", new HelpMessage("makepoll", "~makepoll <name> <\"[option 1]\" \"[option 2]\" \"[etc...]\">"), '~', perm));
            close    = ChatRoom.AddCommand(this, new Command(this, "closepoll", new HelpMessage("closepoll", "~closepoll"), '~', perm));
            _winner  = ChatRoom.AddCommand(this, new Command(this, "pollwinner", new HelpMessage("pollwinner", "~pollwinner"), '~', new[] { BadgeType.None }));
            _current = ChatRoom.AddCommand(this, new Command(this, "currentpoll", new HelpMessage("currentpoll", "~currentpoll"), '~', new[] { BadgeType.None }));
            
            make    .OnCommandEvent  += Make_OnCommandEvent;
            close   .OnCommandEvent  += Close_OnCommandEvent;
            _winner .OnCommandEvent  += Winner_OnCommandEvent;
            _current.OnCommandEvent  += _current_OnCommandEvent;
            
            ChatRoom.AllCommandEvent += ChatRoom_AllCommandEvent;
        }

        bool pollOpen;
        Poll current;
        Option winner;
        List<string> user = new List<string>();
        List<Option> chosen = new List<Option>();

        private void ChatRoom_AllCommandEvent(object sender, ChatRoom.AllCommandEventArgs e)
        {
            if (pollOpen)
            {
                for (int i = 0; i < current.options.Count; i++)
                {
                    if (!user.Contains(e.Username) && e.CommandName.ToLower() == current.options[i].Name.ToLower())
                    {
                        current.options[i].votes++;
                        user.Add(e.Username);
                        Chat.SendMessage("Vote cast.");
                        break;
                    }
                }
            }
        }

        private void _current_OnCommandEvent(object sender, Command.CommandArgs e)
        {
            if (pollOpen)
            {
                string names = "";
                for (int i = 0; i < current.options.Count; i++)
                {
                    names += "\"" + current.options[i].Name + "\" ";
                }
                names = names.TrimEnd(' ');
                Chat.SendMessage($"{current.name}, poll options: {names}");
            }
        }
        private void Winner_OnCommandEvent(object sender, Command.CommandArgs e)
        {
            pollOpen = false;
            if (winner == null) Chat.SendMessage("No poll has been run yet.");
            else Chat.SendMessage(string.Format("\"{0}\" option won with {1} votes.", winner.Name.ToLowerInvariant(), winner.votes));
        }
        private void Close_OnCommandEvent(object sender, Command.CommandArgs e)
        {
            if (pollOpen && (e.BadgeFlag[BadgeID.BC] || e.BadgeFlag[BadgeID.Mod]))
            {
                Option choice = new Option() { Name = "default", votes = 0 };
                for (int i = 0; i < current.options.Count; i++)
                {
                    if (current.options[i].votes > choice.votes)
                    {
                        chosen.Clear();
                        chosen.Add(current.options[i]);
                        choice = current.options[i];
                    }
                    else if (current.options[i].votes == choice.votes)
                    {
                        chosen.Add(current.options[i]);
                    }
                }
                if (chosen.Count > 1)
                {
                    current = Poll.NewPoll("Tie-Breaker", chosen);
                    user.Clear();
                    Chat.SendMessage("Tie breaker started.");
                    _current_OnCommandEvent(sender, e);
                }
                else
                {
                    winner = choice;
                    Winner_OnCommandEvent(sender, e);
                }
            }
        }
        private void Make_OnCommandEvent(object sender, Command.CommandArgs e)
        {
            if (!pollOpen && e.Message.Contains(" ") && (e.BadgeFlag[BadgeID.BC] || e.BadgeFlag[BadgeID.Mod]))
            {
                string cmd = e.Message.Substring(e.CommandName.Length + 1);
                string name = cmd.Substring(0, cmd.IndexOf(' ') - 1);
                List <Option> option = new List<Option>();
                for (int i = 1; i < cmd.Length - 1; i++)
                {
                    if (cmd[i] == '"' && cmd[i + 1] != ' ')
                    {
                        string opt = cmd.Substring(i + 1);
                        opt = opt.Substring(0, opt.IndexOf('"'));
                        option.Add(new Option()
                        {
                            Name = opt
                        });
                    }
                }
                current = Poll.NewPoll(name, option);
                user.Clear();
                pollOpen = true;
                Chat.SendMessage(string.Format("{0} poll started -- cast your votes prefaced by the \"~\" character.", name));
                _current_OnCommandEvent(sender, e);
            }
        }
    }

    public class Poll
    {
        public string name;
        public List<Option> options = new List<Option>();
        public static Poll NewPoll(string name, List<Option> options)
        {
            Poll poll = new Poll();
            poll.name = name;
            poll.options = options;
            return poll;
        }
    }
    public class Option
    {
        public string Name;
        public int votes;
    }
}
