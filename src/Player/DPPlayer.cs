using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FLServer.CharacterDB;
using FLServer.Chat;
using FLServer.DataWorkers;
using FLServer.Object;
using FLServer.Object.Base;
using FLServer.Object.Solar;
using FLServer.Player.PlayerExtensions;
using FLServer.Ship;
using FLServer.Solar;
using Ionic.Zlib;

namespace FLServer.Player
{
    public interface IPlayerState
    {
        string StateName();
        void EnterState(Player player);
        void RxMsgFromClient(Player player, byte[] msg);
    };


    public class Player : CharacterData
    {



        public enum ChatCommand
        {
// ReSharper disable InconsistentNaming
            GROUPINVITEREQUEST = 0,
            GROUPLEAVEREQUEST = 1,
            GROUPINVITATIONACCEPTEDREQUEST = 2,
            GROUPINVITATIONSENT = 3,
            GROUPINVITATIONRECEIVED = 4,
            GROUPJOINED = 5,
            NEWGROUPMEMBER = 6,
            GROUPLEFT = 7,
            GROUPMEMBERLEFT = 8
// ReSharper restore InconsistentNaming
        }

        public enum MiscObjUpdateType
        {
// ReSharper disable InconsistentNaming
            RANK,
            SYSTEM,
            GROUP,
            UNK2,
            UNK3,
            NEWS,
// ReSharper restore InconsistentNaming
        }

        public enum PopupDialogButtons
        {
// ReSharper disable InconsistentNaming
            POPUPDIALOG_BUTTONS_LEFT_YES = 1,
            POPUPDIALOG_BUTTONS_CENTER_NO = 2,
            POPUPDIALOG_BUTTONS_RIGHT_LATER = 4,
            POPUPDIALOG_BUTTONS_CENTER_OK = 8
// ReSharper restore InconsistentNaming
        }

        /// <summary>
        ///     The accountid of the player.
        /// </summary>
        public string AccountID;

        /// <summary>
        ///     The ID of this player on the proxy server.
        /// </summary>
        public Session DPSess;

        /// <summary>
        ///     The FL player ID.
        /// </summary>
        public uint FLPlayerID;

        /// <summary>
        ///     The player's Group.
        /// </summary>
        public Group Group;

        /// <summary>
        ///     The Group the player is currently invited to.
        /// </summary>
        public Group GroupInvited;

        /// <summary>
        ///     The log message receiver.
        /// </summary>
        public ILogController Log;

        /// <summary>
        ///     We send notification of state changes to this player for these objects.
        /// </summary>
        public Dictionary<uint, SimObject> MonitoredObjs = new Dictionary<uint, SimObject>();

        /// <summary>
        ///     The connection to the proxy freelancer server.
        /// </summary>
        public volatile DPGameRunner Runner;

        /// <summary>
        ///     The state of the connection.
        /// </summary>
        private IPlayerState _state;


        /// <summary>
        ///     At object creation time we assume that the freelancer player has connected to the
        ///     proxy server and is expecting the normal freelancer server login sequence. This
        ///     class manages this message exchange until they select a character at which point
        ///     the controller will establish a connection to a slave freelancer server and
        ///     forward traffic between the two.
        /// </summary>
        /// <param name="dplayid"></param>
        /// <param name="log"></param>
        /// <param name="flplayerid"></param>
        /// <param name="runner"></param>
        public Player(Session dplayid, ILogController log, uint flplayerid, DPGameRunner runner)
        {
            DPSess = dplayid;
            Log = log;
            FLPlayerID = flplayerid;
            Runner = runner;
            Ship = new Ship.Ship(runner) {player = this};
            Wgrp = new WeaponGroup();

            _state = DPCLoginState.Instance();
            _state.EnterState(this);
        }

        public void SetState(IPlayerState newstate)
        {
            if (_state != newstate)
            {
                Log.AddLog(LogType.GENERAL, "change state: old={0} new={1}", _state.StateName(), newstate.StateName());
                _state = newstate;
                _state.EnterState(this);
                if (_state.StateName() == "in-base-state")
                {
                    Ship.IsDestroyed = false;
                }

                
            }
        }

        public void RxMsgFromClient(byte[] msg)
        {
            _state.RxMsgFromClient(this, msg);
        }

        public void SendMiscObjUpdate(MiscObjUpdateType update, string msg)
        {
            switch (update)
            {
                case MiscObjUpdateType.NEWS: //  news?
                {
                    Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_MISCOBJUPDATE type=news msg=\"{0}\"", msg);
                    byte[] omsg = {0x54, 0x02, 0x10, 0x00};
                    FLMsgType.AddUnicodeStringLen16(ref omsg, msg);
                    SendMsgToClient(omsg);
                    return;
                }
            }
        }

        public void SendMiscObjUpdate(MiscObjUpdateType update, params uint[] values)
        {
            switch (update)
            {
                case MiscObjUpdateType.RANK:
                {
                    Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_MISCOBJUPDATE type=rank playerid={0} rank={1}",
                        values[0], values[1]);
                    byte[] omsg = {0x54, 0x02, 0x44, 0x00};
                    FLMsgType.AddUInt32(ref omsg, values[0]);
                    FLMsgType.AddUInt16(ref omsg, values[1]);
                    SendMsgToClient(omsg);
                    return;
                }
                case MiscObjUpdateType.SYSTEM:
                {
                    Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_MISCOBJUPDATE type=system playerid={0} systemid={1}",
                        values[0], values[1]);
                    byte[] omsg = {0x54, 0x02, 0x84, 0x00};
                    FLMsgType.AddUInt32(ref omsg, values[0]);
                    FLMsgType.AddUInt32(ref omsg, values[1]);
                    SendMsgToClient(omsg);
                    return;
                }
                case MiscObjUpdateType.GROUP:
                {
                    Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_MISCOBJUPDATE type=group playerid={0} group={1}",
                        values[0], values[1]);
                    byte[] omsg = {0x54, 0x02, 0x05, 0x00};
                    FLMsgType.AddUInt32(ref omsg, values[0]);
                    FLMsgType.AddUInt32(ref omsg, values[1]);
                    SendMsgToClient(omsg);
                    return;
                }
                case MiscObjUpdateType.UNK2:
                {
                    Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_MISCOBJUPDATE type=unknown2 objid={0}", values[0]);
                    byte[] omsg = {0x54, 0x02, 0x28, 0x00};
                    FLMsgType.AddUInt32(ref omsg, values[0]);
                    FLMsgType.AddInt32(ref omsg, -1); // faction?
                    SendMsgToClient(omsg);
                    return;
                }
                case MiscObjUpdateType.UNK3:
                {
                    Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_MISCOBJUPDATE type=unknown3 objid={0}", values[0]);
                    byte[] omsg = {0x54, 0x02, 0x09, 0x00};
                    FLMsgType.AddUInt32(ref omsg, values[0]);
                    FLMsgType.AddUInt32(ref omsg, 0);
                    SendMsgToClient(omsg);
                    return;
                }
                default:
                    return;
            }
        }

        /// <summary>
        ///     Send an update to the player list when a player has selected a
        ///     character, changed system or rank. Is this only valid if a
        ///     character has been selected
        /// </summary>
        /// <param name="player"></param>
        public void SendPlayerListUpdate(DPGameRunner.PlayerListItem player)
        {
            if (player.Name != null)
            {
                // Send Group
                SendMiscObjUpdate(MiscObjUpdateType.GROUP, player.FlPlayerID, player.Group == null ? 0 : player.Group.ID);

                // Send rank
                SendMiscObjUpdate(MiscObjUpdateType.RANK, player.FlPlayerID, player.Rank);

                // Send system
                SendMiscObjUpdate(MiscObjUpdateType.SYSTEM, player.FlPlayerID, player.System.SystemID);

                // TODO: ?
                // Send affiliation/faction information
                //{
                //    log.AddLog(String.Format("tx FLPACKET_SERVER_MISCOBJUPDATE type=faction playerid={0} faction={1}", player.flplayerid, 0));
                //    byte[] omsg = { 0x54, 0x02, 0x24, 0x00 };
                //    FLMsgType.AddUInt32(ref omsg, player.flplayerid);
                //    FLMsgType.AddUInt32(ref omsg, 0xFFFFFFFF); // player.ship.faction.factionid);
                //    SendMsgToClient(omsg);
                //}
            }
        }

        /// <summary>
        ///     Send an update to the player list when a player has left the server.
        /// </summary>
        /// <param name="player"></param>
        public void SendPlayerListDepart(Player player)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_PLAYERLIST playerid={0}", player.FLPlayerID);
            {
                byte[] omsg = {0x52, 0x02};
                FLMsgType.AddUInt32(ref omsg, 2); // command: 1 = new, 2 = depart
                FLMsgType.AddUInt32(ref omsg, player.FLPlayerID);
                FLMsgType.AddUInt8(ref omsg, 0);
                FLMsgType.AddUInt8(ref omsg, 0);
                SendMsgToClient(omsg);
            }
        }

        /// <summary>
        ///     Send an update to the player list when a player has joined the server or selected a char
        /// </summary>
        private void SendPlayerListJoin(Player player, bool hide)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_PLAYERLIST playerid={0}", player.FLPlayerID);
            // Player list and character name
            {
                byte[] omsg = {0x52, 0x02};
                FLMsgType.AddUInt32(ref omsg, 1); // 1 = new, 2 = depart
                FLMsgType.AddUInt32(ref omsg, player.FLPlayerID); // player id
                FLMsgType.AddUInt8(ref omsg, hide ? 1u : 0u); // hide 1 = yes, 0 = no
                FLMsgType.AddUnicodeStringLen8(ref omsg, player.Name + "\0");
                SendMsgToClient(omsg);
            }
        }

        /// <summary>
        ///     Send the current player list information to this player for
        ///     all player online.
        /// </summary>
        public void SendCompletePlayerList()
        {
            // FLPACKET_SERVER_PLAYERLIST
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_PLAYERLIST");

            // Reset the player list. I'm not certain that this is necessary.
            {
                byte[] omsg = {0x52, 0x02};
                FLMsgType.AddUInt32(ref omsg, 0);
                FLMsgType.AddUInt32(ref omsg, 0);
                FLMsgType.AddUInt8(ref omsg, 0);
                FLMsgType.AddUInt8(ref omsg, 0);
                SendMsgToClient(omsg);
            }

            // Send player status to the player for all players including this one.
            foreach (DPGameRunner.PlayerListItem item in Runner.Playerlist.Values)
            {
                SendPlayerListJoin(item.Player, true);
                SendPlayerListUpdate(item);
            }
        }

        public void SendMsgToClient(byte[] msg)
        {
            Runner.SendMessage(this, msg);
        }

        // FLPACKET_COMMON_SET_VISITED_STATE
        public void SendSetVisitedState()
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_COMMON_SET_VISITED_STATE");

            byte[] omsg = {0x13, 0x01};
            FLMsgType.AddInt32(ref omsg, 4 + (Visits.Count*5));
            FLMsgType.AddInt32(ref omsg, Visits.Count);
            foreach (var pi in Visits)
            {
                FLMsgType.AddUInt32(ref omsg, pi.Key);
                FLMsgType.AddUInt8(ref omsg, pi.Value);
            }

            SendMsgToClient(omsg);
        }

        // FLPACKET_COMMON_SET_MISSION_LOG
        public void SendSetMissionLog()
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_COMMON_SET_MISSION_LOG");

            byte[] omsg = {0x19, 0x01};
            FLMsgType.AddUInt32(ref omsg, 8); // cnt * 4 
            FLMsgType.AddUInt32(ref omsg, 1); // 1 
            FLMsgType.AddUInt32(ref omsg, 0); // 0
            SendMsgToClient(omsg);
        }

        // FLPACKET_COMMON_SET_INTERFACE_STATE
        public void SendSetInterfaceState()
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_COMMON_SET_INTERFACE_STATE");

            byte[] omsg = {0x1c, 0x01};
            FLMsgType.AddUInt32(ref omsg, 1); // cnt
            FLMsgType.AddUInt32(ref omsg, 3); // state 3
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_SETREPUTATION
        /// <summary>
        ///     Set the attitude and faction of an object with respect to this solar
        /// </summary>
        public void SendSetReputation(Object.Solar.Solar solar)
        {
            float attitude = 0;
            if (solar.Faction.FactionID != 0xFFFFFFFF)
            {
                attitude = Ship.GetAttitudeTowardsFaction(solar.Faction);
            }

            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_SETREPUTATION solar.objid={0} faction={1} attitude={2}",
                solar.Objid, solar.Faction.FactionID, attitude);

            byte[] omsg = {0x29, 0x02, 0x01};
            FLMsgType.AddUInt32(ref omsg, solar.Objid);
            FLMsgType.AddUInt32(ref omsg, solar.Faction.FactionID);
            FLMsgType.AddFloat(ref omsg, attitude);
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_SETREPUTATION
        /// <summary>
        ///     Set the attitude and faction of an object with respect to this player's ship
        /// </summary>
        public void SendSetReputation(Ship.Ship ship)
        {
            byte[] omsg = {0x29, 0x02};
            // If the ship doesn't know about this faction then add it
            // with the default faction affiliaton from the initialworld
            // settings
            if (!Ship.Reps.ContainsKey(ship.faction))
                Ship.Reps[ship.faction] = 0.0f; //fixme solar.faction.default_rep;

            float attitude = Ship.Reps[ship.faction];

            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_SETREPUTATION solar.objid={0} faction={1} attitude={2}",
                ship.Objid, ship.faction.Nickname, attitude);

            FLMsgType.AddUInt8(ref omsg, 0x01);
            FLMsgType.AddUInt32(ref omsg, ship.Objid);
            FLMsgType.AddUInt32(ref omsg, ship.faction.FactionID);
            FLMsgType.AddFloat(ref omsg, attitude);
            SendMsgToClient(omsg);
        }

        /// <summary>
        /// </summary>
        public void SendInitSetReputation()
        {
            byte[] omsg = {0x29, 0x02};
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_SETREPUTATION self");
            FLMsgType.AddUInt8(ref omsg, 0x01);
            FLMsgType.AddUInt32(ref omsg, FLPlayerID);
            FLMsgType.AddUInt32(ref omsg, 0);
            FLMsgType.AddFloat(ref omsg, 0); // rep
            SendMsgToClient(omsg);
        }

        public void SendPopupDialog(FLFormatString caption, FLFormatString message, PopupDialogButtons buttons)
        {
            byte[] omsg = {0x1B, 0x01};
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_COMMON_POP_UP_DIALOG");
            FLMsgType.AddArray(ref omsg, caption.GetBytes());
            FLMsgType.AddArray(ref omsg, message.GetBytes());
            FLMsgType.AddUInt32(ref omsg, (uint) buttons);
            SendMsgToClient(omsg);
        }

        /// <summary>
        ///     Send a chat message to the client.
        /// </summary>
        /// <param name="command"></param>
        public void SendChat(byte[] command)
        {
            byte[] omsg = {0x05, 0x01};
            FLMsgType.AddInt32(ref omsg, command.Length);
            FLMsgType.AddArray(ref omsg, command);
            FLMsgType.AddUInt32(ref omsg, 0);
            FLMsgType.AddUInt32(ref omsg, 0);
            SendMsgToClient(omsg);
        }


        public void SendChatCommand(ChatCommand command, uint playerId)
        {
            byte[] omsg = {0x05, 0x01, 0x08, 0x00, 0x00, 0x00};

            FLMsgType.AddUInt32(ref omsg, (uint) command);
            FLMsgType.AddUInt32(ref omsg, playerId);
            FLMsgType.AddUInt32(ref omsg, FLPlayerID);
            FLMsgType.AddUInt32(ref omsg, 0x10004);

            SendMsgToClient(omsg);
        }

        /// <summary>
        ///     Send an infocard update to the client using the dsace protocol.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="text"></param>
        public void SendInfocardUpdate(uint id, string text)
        {
            var isrc = new byte[0];
            FLMsgType.AddUInt32(ref isrc, 0); // reset all cards flag
            FLMsgType.AddUInt32(ref isrc, 1); // number of infocards
            FLMsgType.AddUInt32(ref isrc, id); // id number
            FLMsgType.AddUnicodeStringLen32(ref isrc, text); // unicode text array with size as uint

            // Compress the infocard list
            byte[] idest;
            using (var ms = new MemoryStream())
            {
                using (var zs = new ZlibStream(ms, CompressionMode.Compress))
                    zs.Write(isrc, 0, isrc.Length);
                idest = ms.ToArray();
            }

            // Pack the compressed infocards into the dsac command.
            var command = new byte[0];
            FLMsgType.AddUInt32(ref command, 0xD5AC);
            FLMsgType.AddUInt32(ref command, 0x01);
            FLMsgType.AddUInt32(ref command, (uint) idest.Length);
            FLMsgType.AddUInt32(ref command, (uint) isrc.Length);
            FLMsgType.AddArray(ref command, idest);
            SendChat(command);
        }

        // FLPACKET_SERVER_CHARSELECTVERIFIED
        public void SendCharSelectVerified()
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_CHARSELECTVERIFIED");

            byte[] omsg = {0x08, 0x02};
            FLMsgType.AddUInt32(ref omsg, FLPlayerID);
            FLMsgType.AddDouble(ref omsg, Runner.GameTime());
            SendMsgToClient(omsg);
        }

        // FLPACKET_COMMON_REQUEST_PLAYER_STATS
        public void SendPlayerStats()
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_COMMON_REQUEST_PLAYER_STATS");

            byte[] omsg = {0x18, 0x01};
            FLMsgType.AddInt32(ref omsg, (13*4) + (Ship.Reps.Count*8) + (Kills.Count*8)); //

            FLMsgType.AddUInt32(ref omsg, 4); // rm_completed
            FLMsgType.AddUInt32(ref omsg, 0); // u_dword
            FLMsgType.AddUInt32(ref omsg, 2); // rm_failed
            FLMsgType.AddUInt32(ref omsg, 0); // u_dword
            FLMsgType.AddFloat(ref omsg, 10000.0f); // total_time_played
            FLMsgType.AddUInt32(ref omsg, 6); // systems_visited
            FLMsgType.AddUInt32(ref omsg, 5); // bases_visited
            FLMsgType.AddUInt32(ref omsg, 4); // holes_visited
            FLMsgType.AddInt32(ref omsg, Kills.Count); // kills_cnt
            FLMsgType.AddUInt32(ref omsg, Ship.Rank); // rank
            FLMsgType.AddUInt32(ref omsg, (UInt32) Money); // current_worth
            FLMsgType.AddUInt32(ref omsg, 0); // dunno
            FLMsgType.AddInt32(ref omsg, Ship.Reps.Count);
            foreach (var pi in Kills)
            {
                FLMsgType.AddUInt32(ref omsg, pi.Key);
                FLMsgType.AddUInt32(ref omsg, pi.Value);
            }
            foreach (var pi in Ship.Reps)
            {
                FLMsgType.AddUInt32(ref omsg, pi.Key.FactionID);
                FLMsgType.AddFloat(ref omsg, pi.Value);
            }

            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_SETSTARTROOM
        public void SendSetStartRoom(uint baseid, uint roomid)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_SETSTARTROOM");
            byte[] omsg = {0x0d, 0x02};
            FLMsgType.AddUInt32(ref omsg, baseid);
            FLMsgType.AddUInt32(ref omsg, roomid);
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_GFMISSIONVENDORWHYEMPTY
        public void SendGFMissionVendorWhyEmpty()
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_GFMISSIONVENDORWHYEMPTY");
            byte[] omsg = {0x5a, 0x02};
            FLMsgType.AddUInt8(ref omsg, 0); // reason
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_GFCOMPLETEMISSIONCOMPUTERLIST
        public void SendGFCompleteMissionComputerList(uint baseid)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_GFCOMPLETEMISSIONCOMPUTERLIST");

            byte[] omsg = {0x10, 0x02};
            FLMsgType.AddUInt32(ref omsg, baseid);
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_GFCOMPLETENEWSBROADCASTLIST
        public void SendGFCompleteNewsBroadcastList(uint baseid)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_NEWS");
            BaseData bd = UniverseDB.FindBase(baseid);

            uint newsid = 0;
            foreach (NewsItem ni in bd.News)
            {
                byte[] omsg = {0x1E, 0x02};
                FLMsgType.AddUInt32(ref omsg, 40 + (uint) ni.Logo.Length); // size goes here

                FLMsgType.AddUInt32(ref omsg, newsid++);
                FLMsgType.AddUInt32(ref omsg, baseid);
                FLMsgType.AddUInt16(ref omsg, 0);

                FLMsgType.AddUInt32(ref omsg, ni.Icon);
                FLMsgType.AddUInt32(ref omsg, ni.Category);
                FLMsgType.AddUInt16(ref omsg, 0);
                FLMsgType.AddUInt32(ref omsg, ni.Headline);
                FLMsgType.AddUInt16(ref omsg, 0);
                FLMsgType.AddUInt32(ref omsg, ni.Text);
                FLMsgType.AddUInt16(ref omsg, 0);
                FLMsgType.AddAsciiStringLen32(ref omsg, ni.Logo);
                FLMsgType.AddUInt32(ref omsg, 0); // unknown hash, 0 seems to work

                SendMsgToClient(omsg);
            }

            // Send "news list complete" message
            {
                byte[] omsg = {0x0e, 0x02};
                FLMsgType.AddUInt32(ref omsg, baseid);
                SendMsgToClient(omsg);
            }
        }

        public void SendPlayerGFUpdateChar(uint roomid, uint charid)
        {
            byte[] omsg = {0x26, 0x02};

            const string movementScript = "scripts\\extras\\player_fidget.thn";
            const string roomLocation = "";

            FLMsgType.AddUInt32(ref omsg,
                74 + (uint) Name.Length + (uint) movementScript.Length + (uint) roomLocation.Length);
            FLMsgType.AddUInt32(ref omsg, charid);
            FLMsgType.AddUInt32(ref omsg, 0x01); // 1 = player, 0 = npc
            FLMsgType.AddUInt32(ref omsg, roomid);
            FLMsgType.AddUInt32(ref omsg, 0); // npc name
            FLMsgType.AddUInt32(ref omsg, Ship.faction.FactionID); // faction
            FLMsgType.AddUnicodeStringLen32(ref omsg, Name);
            FLMsgType.AddUInt32(ref omsg, Ship.com_head);
            FLMsgType.AddUInt32(ref omsg, Ship.com_body);
            FLMsgType.AddUInt32(ref omsg, Ship.com_lefthand);
            FLMsgType.AddUInt32(ref omsg, Ship.com_righthand);
            FLMsgType.AddUInt32(ref omsg, 0); // accessories count + list
            FLMsgType.AddAsciiStringLen32(ref omsg, movementScript);
            FLMsgType.AddInt32(ref omsg, -1); // behaviourid

            if (roomLocation.Length == 0)
                FLMsgType.AddInt32(ref omsg, -1);
            else
                FLMsgType.AddAsciiStringLen32(ref omsg, roomLocation);

            FLMsgType.AddUInt32(ref omsg, 0x01); // 1 = player, 0 = npc
            FLMsgType.AddUInt32(ref omsg, 0x00); // 1 = sitlow, 0 = stand
            FLMsgType.AddUInt32(ref omsg, Ship.voiceid); // voice

            SendMsgToClient(omsg);
        }

        public void SendNPCGFUpdateChar(BaseCharacter ch, uint charid)
        {
            byte[] omsg = {0x26, 0x02};

            FLMsgType.AddUInt32(ref omsg, 68 + (uint) ch.FidgetScript.Length + (uint) ch.RoomLocation.Length);
            FLMsgType.AddUInt32(ref omsg, charid);
            FLMsgType.AddUInt32(ref omsg, 0); // 1 = player, 0 = npc
            FLMsgType.AddUInt32(ref omsg, ch.RoomID);
            FLMsgType.AddUInt32(ref omsg, ch.IndividualName); // npc name
            FLMsgType.AddUInt32(ref omsg, ch.Faction.FactionID); // faction
            FLMsgType.AddInt32(ref omsg, -1);
            FLMsgType.AddUInt32(ref omsg, ch.Head);
            FLMsgType.AddUInt32(ref omsg, ch.Body);
            FLMsgType.AddUInt32(ref omsg, ch.Lefthand);
            FLMsgType.AddUInt32(ref omsg, ch.Righthand);
            FLMsgType.AddUInt32(ref omsg, 0); // accessories count + list
            FLMsgType.AddAsciiStringLen32(ref omsg, ch.FidgetScript);
            FLMsgType.AddUInt32(ref omsg, charid); // behaviourid

            if (ch.RoomLocation.Length == 0)
                FLMsgType.AddInt32(ref omsg, -1);
            else
                FLMsgType.AddAsciiStringLen32(ref omsg, ch.RoomLocation);

            FLMsgType.AddUInt32(ref omsg, 0x00); // 1 = player, 0 = npc
            FLMsgType.AddUInt32(ref omsg, 0x00); // 1 = sitlow, 0 = stand
            FLMsgType.AddUInt32(ref omsg, ch.Voice); // voice

            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_GFCOMPLETECHARLIST
        public void SendGFCompleteCharList(uint roomid)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_GFCOMPLETECHARLIST");

            // Send the player 'trent' character
            uint charid = 1;
            SendPlayerGFUpdateChar(roomid, charid++);

            // Send the barman, dealers, etc. These have fixed locations and
            // fidget scripts
            foreach (BaseCharacter ch in Ship.Basedata.Chars.Values)
            {
                if (ch.RoomID == roomid && ch.Type != null)
                {
                    SendNPCGFUpdateChar(ch, charid);
                    SendNPCGFUpdateScripts(ch, charid);
                    charid++;
                }
            }

            // Send the barflies

            // Send "char list complete message"
            {
                byte[] omsg = {0x0f, 0x02};
                FLMsgType.AddUInt32(ref omsg, roomid);
                SendMsgToClient(omsg);
            }
        }

        public void SendNPCGFUpdateScripts(BaseCharacter npc, uint charid)
        {
            List<string> scripts = News.GetScriptsForNPCInteraction(this, npc);

            //int scriptSize = 0;
            //foreach (string script in scripts)
                //scriptSize += 8 + script.Length;
            var scriptSize = scripts.Sum(script => 8 + script.Length);

            byte[] omsg = {0x1A, 0x02};
            FLMsgType.AddUInt32(ref omsg, 56 + (uint) scriptSize); // size
            FLMsgType.AddUInt32(ref omsg, charid); // behaviourid
            FLMsgType.AddUInt32(ref omsg, npc.RoomID);

            FLMsgType.AddUInt8(ref omsg, 0);
            FLMsgType.AddUInt8(ref omsg, 0);
            FLMsgType.AddUInt8(ref omsg, 1);
            FLMsgType.AddUInt32(ref omsg, 0);
            FLMsgType.AddUInt32(ref omsg, 0);
            FLMsgType.AddUInt8(ref omsg, 1);

            FLMsgType.AddUInt32(ref omsg, 0); // count for some strings (maybe locations?) that don't appear to be used

            FLMsgType.AddInt32(ref omsg, scripts.Count); // count for behaviour scripts
            foreach (string script in scripts)
            {
                FLMsgType.AddAsciiStringLen32(ref omsg, script); // script name
                FLMsgType.AddUInt32(ref omsg, 0); // script path level
            }

            FLMsgType.AddUInt32(ref omsg, 0); // talkid (nothing, bribe, mission, rumor, info)
            FLMsgType.AddUInt32(ref omsg, charid); // charid

            FLMsgType.AddUInt32(ref omsg, 1); // dunno

            FLMsgType.AddUInt32(ref omsg, 0); // resourceid
            FLMsgType.AddUInt16(ref omsg, 0); // count
            FLMsgType.AddUInt32(ref omsg, 0); // resourceid
            FLMsgType.AddUInt16(ref omsg, 0); // count
            FLMsgType.AddUInt32(ref omsg, 0); // dunno

            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_GFCOMPLETESCRIPTBEHAVIORLIST
        public void SendGFCompleteScriptBehaviourList(uint roomid)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_GFCOMPLETESCRIPTBEHAVIORLIST");

            {
                byte[] omsg = {0x11, 0x02};
                FLMsgType.AddUInt32(ref omsg, roomid);
                SendMsgToClient(omsg);
            }
        }

        // FLPACKET_SERVER_GFCOMPLETEAMBIENTSCRIPTLIST
        public void SendGFCompleteAmbientScriptList(uint roomid)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_GFCOMPLETEAMBIENTSCRIPTLIST");

            byte[] omsg = {0x13, 0x02};
            FLMsgType.AddUInt32(ref omsg, roomid);
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_SETADDITEM
        public void SendAddItem(uint goodid, uint hpid, uint count, float health, bool mounted, string hpname)
        {
            Log.AddLog(LogType.FL_MSG,
                "tx FLPACKET_SERVER_SETADDITEM goodid={0} hpid={1} count={2} health={3} mounted={4} hpname={5}",
                goodid, hpid, count, health, mounted, hpname);

            byte[] omsg = {0x2E, 0x02};
            FLMsgType.AddUInt32(ref omsg, goodid);
            FLMsgType.AddUInt32(ref omsg, hpid);
            FLMsgType.AddUInt32(ref omsg, count);
            FLMsgType.AddFloat(ref omsg, health);
            FLMsgType.AddUInt32(ref omsg, (mounted ? 1u : 0u));
            FLMsgType.AddUInt16(ref omsg, 0);
            if (hpname.Length > 0)
                FLMsgType.AddAsciiStringLen32(ref omsg, hpname + "\0");
            else if (mounted)
                FLMsgType.AddAsciiStringLen32(ref omsg, "");
            else
                FLMsgType.AddAsciiStringLen32(ref omsg, "BAY\0");

            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_SETREMOVEITEM
        public void SendRemoveItem(ShipItem item)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_SETREMOVEITEM hpid={0}", item.hpid);
            byte[] omsg = {0x2F, 0x02};
            FLMsgType.AddUInt32(ref omsg, item.hpid);
            FLMsgType.AddUInt32(ref omsg, 0);
            FLMsgType.AddUInt32(ref omsg, 0);
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_SETEQUIPMENT
        public void SendSetEquipment(Dictionary<uint, ShipItem> items)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_SETEQUIPMENT count={0}", items.Count);
            byte[] omsg = {0x24, 0x02};
            FLMsgType.AddUInt16(ref omsg, (uint) items.Count);
            foreach (ShipItem item in items.Values)
            {
                FLMsgType.AddUInt32(ref omsg, item.count);
                FLMsgType.AddFloat(ref omsg, item.health);
                FLMsgType.AddUInt32(ref omsg, item.arch.ArchetypeID);
                FLMsgType.AddUInt16(ref omsg, item.hpid);
                FLMsgType.AddUInt16(ref omsg, (item.mounted ? 1u : 0u));
                if (item.hpname.Length > 0)
                    FLMsgType.AddAsciiStringLen16(ref omsg, item.hpname + "\0");
                else if (item.mounted)
                    FLMsgType.AddAsciiStringLen16(ref omsg, "");
                else
                    FLMsgType.AddAsciiStringLen16(ref omsg, "BAY\0");
            }
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_SETCASH
        public void SendSetMoney()
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_SETCASH money={0}", Money);
            byte[] omsg = {0x30, 0x02};
            if (Money > 2000000000)
                FLMsgType.AddInt32(ref omsg, 2000000000);
            else
                FLMsgType.AddInt32(ref omsg, Money);
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_REQUESTCREATESHIPRESP
        public void SendCreateShipResponse(Ship.Ship ship)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_REQUESTCREATESHIPRESP objid={0}", ship.Objid);
            byte[] omsg = {0x27, 0x02};
            FLMsgType.AddUInt8(ref omsg, 1); // dunno
            FLMsgType.AddUInt32(ref omsg, ship.Objid);
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_LAUNCH
        public void SendServerLaunch()
        {
            {
                Vector eulerRot = Matrix.MatrixToEulerDeg(Ship.Orientation);
                Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_SERVERLAUNCH objid={0} position={1} orient={2}",
                    Ship.Objid, Ship.Position, eulerRot);

                // If we're spawning in a base solar, send the solar id
                if (Ship.CurrentAction is LaunchFromBaseAction)
                {
                    var action = Ship.CurrentAction as LaunchFromBaseAction;

                    Ship.Position = action.Position;
                    Ship.Orientation = Quaternion.QuaternionToMatrix(action.Orientation);

                    byte[] omsg = {0x07, 0x02};
                    FLMsgType.AddUInt32(ref omsg, Ship.Objid);
                    FLMsgType.AddUInt32(ref omsg, action.DockingObj.Solar.Objid);
                    FLMsgType.AddUInt32(ref omsg, action.DockingObj.Index);
                    FLMsgType.AddFloat(ref omsg, (float) Ship.Position.x);
                    FLMsgType.AddFloat(ref omsg, (float) Ship.Position.y);
                    FLMsgType.AddFloat(ref omsg, (float) Ship.Position.z);
                    Quaternion q = Quaternion.MatrixToQuaternion(Ship.Orientation);
                    FLMsgType.AddFloat(ref omsg, (float) q.W);
                    FLMsgType.AddFloat(ref omsg, (float) q.I);
                    FLMsgType.AddFloat(ref omsg, (float) q.J);
                    FLMsgType.AddFloat(ref omsg, (float) q.K);
                    SendMsgToClient(omsg);

                    action.DockingObj.Activate(Runner, Ship);
                }
                    // Otherwise we're spawning in a space or at a jump/moor point
                else if (Ship.CurrentAction is LaunchInSpaceAction)
                {
                    var action = Ship.CurrentAction as LaunchInSpaceAction;

                    Ship.Position = action.Position;
                    Ship.Orientation = Quaternion.QuaternionToMatrix(action.Orientation);

                    byte[] omsg = {0x07, 0x02};
                    FLMsgType.AddUInt32(ref omsg, Ship.Objid);
                    FLMsgType.AddUInt32(ref omsg, 0);
                    FLMsgType.AddInt32(ref omsg, -1);
                    FLMsgType.AddFloat(ref omsg, (float) Ship.Position.x);
                    FLMsgType.AddFloat(ref omsg, (float) Ship.Position.y);
                    FLMsgType.AddFloat(ref omsg, (float) Ship.Position.z);
                    FLMsgType.AddFloat(ref omsg, (float) action.Orientation.W);
                    FLMsgType.AddFloat(ref omsg, (float) action.Orientation.I);
                    FLMsgType.AddFloat(ref omsg, (float) action.Orientation.J);
                    FLMsgType.AddFloat(ref omsg, (float) action.Orientation.K);
                    SendMsgToClient(omsg);
                }
            }

            {
                byte[] omsg = {0x54, 0x02, 0x09, 0x00}; // flag (objid + dunno)
                FLMsgType.AddUInt32(ref omsg, Ship.Objid);
                FLMsgType.AddUInt32(ref omsg, 0); // dunnno
                SendMsgToClient(omsg);
            }

            {
                byte[] omsg = {0x54, 0x02, 0x28, 0x00}; // flag (faction + objid)
                FLMsgType.AddUInt32(ref omsg, Ship.Objid);
                FLMsgType.AddFloat(ref omsg, 0); // faction
                SendMsgToClient(omsg);
            }


            Ship.Basedata = null;
        }

        public void SendServerRequestReturned(Ship.Ship ship, DockingObject dockingObj)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_REQUEST_RETURNED");

            byte[] omsg = {0x44, 0x02};
            FLMsgType.AddUInt32(ref omsg, ship.Objid);
            if (dockingObj != null)
            {
                if (dockingObj.Type == DockingPoint.DockingSphere.TRADELANE_RING)
                    FLMsgType.AddUInt8(ref omsg, 2); // type? 0 is used for docking, 1 for something? else
                else
                    FLMsgType.AddUInt8(ref omsg, 0); // type? 0 is used for docking, 1 for something? else

                FLMsgType.AddUInt8(ref omsg, 4); // 4 = dock, 3 = wait, 5 = denied?
                FLMsgType.AddUInt8(ref omsg, dockingObj.Index); // docking point
            }
            else
            {
                FLMsgType.AddUInt8(ref omsg, 0); // type? 0 is used for docking, 1 for something? else
                FLMsgType.AddUInt8(ref omsg, 0);
                // Response: 5 is dock, 0 is denied target hostile, 2 is denied too big (hostile takes priority), 3 is queue, 4 is proceed after queue; 0, 1 don't actually give a message, 2 gives a generic "denied" message
                FLMsgType.AddUInt8(ref omsg, 255); // docking point
            }
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_USE_ITEM
        public void SendUseItem(uint objid, uint hpid, uint count)
        {
            Log.AddLog(LogType.FL_MSG, "tx[{0}] FLPACKET_SERVER_USE_ITEM objid={1} hpid={2} count={3}", FLPlayerID,
                objid, hpid, count);

            byte[] omsg = {0x51, 0x02};
            FLMsgType.AddUInt32(ref omsg, objid);
            FLMsgType.AddUInt16(ref omsg, hpid);
            FLMsgType.AddUInt16(ref omsg, count);
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_LAND
        public void SendServerLand(Ship.Ship ship, uint solarid, uint baseid)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_LAND objid={0} targetid={1} baseid={2}", ship.Objid, solarid,
                baseid);

            byte[] omsg = {0x0B, 0x02};
            FLMsgType.AddUInt32(ref omsg, ship.Objid);
            FLMsgType.AddUInt32(ref omsg, solarid);
            FLMsgType.AddUInt32(ref omsg, baseid);
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_SYSTEM_SWITCH_OUT
        public void SendSystemSwitchOut(Ship.Ship ship, Object.Solar.Solar solar)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_SYSTEM_SWITCH_OUT objid={0} solar={1}", ship.Objid,
                solar.Objid);

            byte[] omsg = {0x21, 0x02};
            FLMsgType.AddUInt32(ref omsg, ship.Objid);
            FLMsgType.AddUInt32(ref omsg, solar.Objid);
            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_SYSTEM_SWITCH_IN
        public void SendSystemSwitchIn(Ship.Ship ship)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_SYSTEM_SWITCH_IN objid={0}", ship.Objid);

            byte[] omsg = {0x22, 0x02};
            FLMsgType.AddUInt32(ref omsg, ship.Objid);
            FLMsgType.AddUInt32(ref omsg, 64);
            FLMsgType.AddFloat(ref omsg, (float) ship.Position.x);
            FLMsgType.AddFloat(ref omsg, (float) ship.Position.y);
            FLMsgType.AddFloat(ref omsg, (float) ship.Position.z);

            Quaternion q = Quaternion.MatrixToQuaternion(ship.Orientation);
            FLMsgType.AddFloat(ref omsg, (float) q.W);
            FLMsgType.AddFloat(ref omsg, (float) q.I);
            FLMsgType.AddFloat(ref omsg, (float) q.J);
            FLMsgType.AddFloat(ref omsg, (float) q.K);

            SendMsgToClient(omsg);
        }

        // FLPACKET_SERVER_SETHULLSTATUS
        public void SendSetHullStatus(Ship.Ship ship)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_SETHULLSTATUS objid={0}", ship.Objid);

            byte[] omsg = {0x49, 0x02};
            FLMsgType.AddFloat(ref omsg, ship.Health);
            SendMsgToClient(omsg);
        }

        //FLPACKET_SERVER_CREATELOOT
        public void SendCreateLoot(Ship.Ship parentShip, Loot loot)
        {
            Log.AddLog(LogType.FL_MSG, "tx FLPACKET_SERVER_CREATELOOT parent objid={0} loot objid={1}", parentShip.Objid,
                loot.Objid);

            byte[] omsg = {0x28, 0x02};
            FLMsgType.AddUInt32(ref omsg, parentShip.Objid); //TODO: Figure out if parent is necessary / what it means
            FLMsgType.AddInt8(ref omsg, 5); //TODO: Reverse meaning
            FLMsgType.AddInt8(ref omsg, 1); //Array size seems to always be one, needs further investigation
            //Array starts, length is count above
            FLMsgType.AddUInt32(ref omsg, loot.Objid);
            FLMsgType.AddUInt16(ref omsg, loot.LootContent.SmallID);
            FLMsgType.AddFloat(ref omsg, loot.LootContentHealth*loot.LootContent.HitPts);
            FLMsgType.AddUInt16(ref omsg, loot.LootContentQuantity);
            FLMsgType.AddUInt16(ref omsg, loot.Arch.SmallID); //Usually a loot crate
            FLMsgType.AddFloat(ref omsg, loot.Health*loot.Arch.HitPts);
            FLMsgType.AddFloat(ref omsg, 0.0f); //TODO: Reverse meaning
            FLMsgType.AddFloat(ref omsg, (float) loot.Position.x);
            FLMsgType.AddFloat(ref omsg, (float) loot.Position.y);
            FLMsgType.AddFloat(ref omsg, (float) loot.Position.z);
            FLMsgType.AddUInt8(ref omsg, loot.MissionFlag1 ? 1u : 0u);
            FLMsgType.AddUInt8(ref omsg, loot.MissionFlag2 ? 1u : 0u);
            SendMsgToClient(omsg);
        }


        /// <summary>
        ///     Process a charinforequest from a player.
        /// </summary>
        public void SendCharInfoRequestResponse()
        {
            var accs = Database.GetAccount(AccountID);
            //string accdir_path = Runner.Server.AcctPath + Path.DirectorySeparatorChar +
                                 //FLMsgType.FLNameToFile(AccountID);
            try
            {
                byte[] omsg = {0x03, 0x02};
                FLMsgType.AddUInt8(ref omsg, 0); // chars
                //foreach (var path in Directory.GetFiles(accdir_path, "??-????????.fl"))
                if (accs != null)
                foreach (var acct in accs)
                {
                    try
                    {
                        var dummy = new CharacterData {Ship = new Ship.Ship(null)};

                        // If the charfile is not valid ignore it.
                        string result = dummy.LoadCharFile(acct, Log);
                        if (result != null)
                        {
                            Log.AddLog(LogType.ERROR, "error: " + result);
                            continue;
                        }

                        FLMsgType.AddAsciiStringLen16(ref omsg, FLMsgType.FLNameToFile(acct.CharName));
                        FLMsgType.AddUInt16(ref omsg, 0);
                        FLMsgType.AddUnicodeStringLen16(ref omsg, acct.CharName);
                        FLMsgType.AddUnicodeStringLen16(ref omsg, "");
                        FLMsgType.AddUInt32(ref omsg, 0);
                        FLMsgType.AddUInt32(ref omsg, 0);
                        FLMsgType.AddUInt32(ref omsg, 0);
                        FLMsgType.AddUInt32(ref omsg, acct.Ship);
                        if (dummy.Money > 2000000000)
                            FLMsgType.AddInt32(ref omsg, 2000000000);
                        else
                            FLMsgType.AddInt32(ref omsg, acct.Money);

                        FLMsgType.AddUInt32(ref omsg, dummy.Ship.System.SystemID);
                        if (dummy.Ship.Basedata != null)
                            FLMsgType.AddUInt32(ref omsg, dummy.Ship.Basedata.BaseID);
                        else
                            FLMsgType.AddUInt32(ref omsg, 0);
                        FLMsgType.AddUInt32(ref omsg, 0);
                        FLMsgType.AddUInt32(ref omsg, dummy.Ship.voiceid);
                        FLMsgType.AddUInt32(ref omsg, dummy.Ship.Rank);
                        FLMsgType.AddUInt32(ref omsg, 0);
                        FLMsgType.AddFloat(ref omsg, dummy.Ship.Health);
                        FLMsgType.AddUInt32(ref omsg, 0);
                        FLMsgType.AddUInt32(ref omsg, 0);
                        FLMsgType.AddUInt32(ref omsg, 0);

                        FLMsgType.AddUInt8(ref omsg, 1);
                        FLMsgType.AddUInt32(ref omsg, dummy.Ship.com_body);
                        FLMsgType.AddUInt32(ref omsg, dummy.Ship.com_head);
                        FLMsgType.AddUInt32(ref omsg, dummy.Ship.com_lefthand);
                        FLMsgType.AddUInt32(ref omsg, dummy.Ship.com_righthand);
                        FLMsgType.AddUInt32(ref omsg, 0);

                        FLMsgType.AddUInt8(ref omsg, 1);
                        FLMsgType.AddUInt32(ref omsg, dummy.Ship.com_body);
                        FLMsgType.AddUInt32(ref omsg, dummy.Ship.com_head);
                        FLMsgType.AddUInt32(ref omsg, dummy.Ship.com_lefthand);
                        FLMsgType.AddUInt32(ref omsg, dummy.Ship.com_righthand);
                        FLMsgType.AddUInt32(ref omsg, 0);

                        FLMsgType.AddUInt8(ref omsg, (uint) dummy.Ship.Items.Count);
                        foreach (ShipItem item in dummy.Ship.Items.Values)
                        {
                            FLMsgType.AddUInt32(ref omsg, item.count);
                            FLMsgType.AddFloat(ref omsg, item.health);
                            FLMsgType.AddUInt32(ref omsg, item.arch.ArchetypeID);
                            FLMsgType.AddUInt16(ref omsg, item.hpid);
                            FLMsgType.AddUInt16(ref omsg, (item.mounted ? 1u : 0u));
                            if (item.hpname.Length > 0)
                                FLMsgType.AddAsciiStringLen16(ref omsg, item.hpname + "\0");
                            else
                                FLMsgType.AddAsciiStringLen16(ref omsg, "BAY\0");
                        }

                        FLMsgType.AddUInt32(ref omsg, 0);

                        omsg[2]++;
                    }
                    catch (Exception e)
                    {
                        Log.AddLog(LogType.ERROR, "error: corrupt file when processing charinforequest '{0}'", e.Message);
                    }
                }
                FLMsgType.AddUInt32(ref omsg, 0);
                SendMsgToClient(omsg);
            }
            catch (Exception e)
            {
                Log.AddLog(LogType.ERROR, "error: unable to process charinforequest '{0}'", e.Message);
            }
        }

        public void SendChat(Rdl rdl)
        {
            Chat.Chat.SendChatToPlayer(this, rdl);
        }

        public void SendWeaponGroup()
        {
            //TODO: send weap group
            // wgrp
        }

        public void Update()
        {
            Runner.Server.AddEvent(new DPGameRunnerPlayerUpdateEvent(this));
        }

        public void OnCharacterSelected(bool sameChar, bool firstLogin)
        {
            SendCompletePlayerList();

            if (sameChar || firstLogin)
            {
                SendPlayerListJoin(this, false);
                foreach (DPGameRunner.PlayerListItem playerListItem in Runner.Playerlist.Values)
                {
                    playerListItem.Player.SendPlayerListJoin(this, playerListItem.FlPlayerID == FLPlayerID);
                }
            }
            else
            {
                foreach (DPGameRunner.PlayerListItem playerListItem in Runner.Playerlist.Values)
                {
                    if (FLPlayerID != playerListItem.FlPlayerID)
                        playerListItem.Player.SendPlayerListDepart(this);
                    playerListItem.Player.SendPlayerListJoin(this, false);
                }
            }

            SendSetVisitedState();
            
            SendSetMissionLog();
            SendSetInterfaceState();
            SendWeaponGroup(); // dunno if position is right
            SendInitSetReputation();
            SendCharSelectVerified();
            SendMiscObjUpdate(MiscObjUpdateType.UNK2, 0);
            SetState(DPCInBaseState.Instance());


            if ((Runner.Server.IntroMsg != null) && firstLogin)
            {
                SendInfocardUpdate(500000, "Welcome to Discovery");

                string intro = Runner.Server.IntroMsg.Replace("$$player$$", Name);
                SendInfocardUpdate(500001, intro);

                SendPopupDialog(new FLFormatString(500000), new FLFormatString(500001),
                    PopupDialogButtons.POPUPDIALOG_BUTTONS_CENTER_OK);
            }
        }
    }
}