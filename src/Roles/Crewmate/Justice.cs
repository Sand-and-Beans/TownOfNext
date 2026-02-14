using AmongUs.GameOptions;
using Hazel;
using TONX.Modules;
using UnityEngine;
using TONX.Roles.Core.Interfaces;
using System.Collections.Generic;

namespace TONX.Roles.Crewmate;

public class Justice : RoleBase, IMeetingButton
{
    public static readonly SimpleRoleInfo RoleInfo = SimpleRoleInfo.Create(
        typeof(Justice),
        player => new Justice(player),
        CustomRoles.Justice,
        () => RoleTypes.Crewmate,
        CustomRoleTypes.Crewmate,
        23500,
        SetupOptionItem,
        "jus|大神官",
        "#FFD700",
        introSound: () => GetIntroSound(RoleTypes.Crewmate)
    );
    
    public Justice(PlayerControl player) : base(RoleInfo, player)
    {
        SelectedPlayers = new List<byte>(2);
        CustomRoleManager.CheckVote.Add(CheckVoteOthers);
    }

    private static OptionItem OptionCanUseTimes;
    enum OptionName
    {
        JusticeCanUseTimes,
    }
    
    private int SkillLimits;
    public List<byte> SelectedPlayers;
    public static byte JusticeScalePlayer = 255;
    

    private static void SetupOptionItem()
    {
        OptionCanUseTimes = IntegerOptionItem.Create(RoleInfo, 11, OptionName.JusticeCanUseTimes, new(1, 15, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Add()
    {
        SkillLimits = OptionCanUseTimes.GetInt();
    }

    public override void OnStartMeeting()
    {
        if (Player.IsAlive())
        {
            Utils.SendMessage(
                string.Format(GetString("JusticeUsesRemaining"), SkillLimits), 
                Player.PlayerId,
                Utils.ColorString(Utils.GetRoleColor(CustomRoles.Justice), GetString("JusticeScaleTitle"))
            );
        }
    }

    public override void AfterMeetingTasks()
    {
        if (JusticeScalePlayer == 255 || !AmongUsClient.Instance.AmHost)
        {
            var result = MeetingVoteManager.Instance.CountVotes(true);
            if (result.IsTie)
                SelectedPlayers.Do(x => Utils.GetPlayerById(x).RpcExile());
        }
        SelectedPlayers.Clear();
        JusticeScalePlayer = 255;
        SendRPC();
    }

    public override void OverrideNameAsSeer(PlayerControl seen, ref string nameText, bool isForMeeting = false)
    {
        if (!Player.IsAlive() || !isForMeeting) return;
        nameText = Utils.ColorString(RoleInfo.RoleColor, seen.PlayerId.ToString()) + " " + nameText;
    }

    public static bool CheckVoteOthers(PlayerControl voter, PlayerControl voted)
    {
        if (JusticeScalePlayer == 255) return true;
        if (voted == null) return false;
        var justiceRole = Utils.GetPlayerById(JusticeScalePlayer).GetRoleClass() as Justice;
        if (justiceRole!.SelectedPlayers.Contains(voted.PlayerId)) return true;
        Utils.SendMessage(GetString("JusticeVoteInvalid"), voter.PlayerId);
        return false;
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(SelectedPlayers.Count);
        foreach (var playerId in SelectedPlayers)
            sender.Writer.Write(playerId);
        sender.Writer.Write(JusticeScalePlayer);
    }
    
    public override void ReceiveRPC(MessageReader reader)
    {
        var count = reader.ReadInt32();
        SelectedPlayers = new List<byte>(count);
        for (var i = 0; i < count; i++)
            SelectedPlayers.Add(reader.ReadByte());
        JusticeScalePlayer = reader.ReadByte();
    }
    
    public string ButtonName => "Scale";
    public bool ShouldShowButton() => Show(Player);
    public bool ShouldShowButtonFor(PlayerControl target) => Show(target);

    private bool Show(PlayerControl player) => SelectedPlayers.Count != 2 && SkillLimits > 0 && player.IsAlive();
    public void OnClickButton(PlayerControl target)
    {
        if (!TrySelectPlayer(target, out var reason))
        {
            Player.ShowPopUp(reason);
            return;
        }
        
        CheckExecuteScale();
    }

    public void OnUpdateButton(MeetingHud meetingHud)
    {
        foreach (var pva in meetingHud.playerStates)
        {
            var btn = pva?.transform?.FindChild("Custom Meeting Button")?.gameObject;
            if (!btn) continue;
            
            if (SelectedPlayers.Contains(pva.TargetPlayerId))
                btn.GetComponent<SpriteRenderer>().color = Color.yellow;
            else if (SelectedPlayers.Count == 2 || SkillLimits <= 0)
            {
                btn.GetComponent<SpriteRenderer>().color = Color.gray;
                btn.GetComponent<PassiveButton>().enabled = false;
            }
            else
                btn.GetComponent<SpriteRenderer>().color = Color.white;
        }
    }
    
    private bool TrySelectPlayer(PlayerControl target, out string reason)
    {
        reason = string.Empty;

        if (SkillLimits <= 0)
        {
            reason = GetString("JusticeLimitMax");
            return false;
        }

        if (SelectedPlayers.Count >= 2)
        {
            reason = GetString("JusticeAlreadyExecuted");
            return false;
        }
        
        if (target == null)
        {
            reason = GetString("JusticePlayerNotFound");
            return false;
        }

        if (!target.IsAlive())
        {
            reason = GetString("JusticeTargetDead");
            return false;
        }

        if (SelectedPlayers.Contains(target.PlayerId))
        {
            SelectedPlayers.Remove(target.PlayerId);
            SendRPC();
            reason = GetString("JusticeSamePlayer");
            return true;
        }

        SelectedPlayers.Add(target.PlayerId);
        SendRPC();
        
        return true;
    }
    private static string MsgToPlayersByID(string msg, out PlayerControl player)
    {
        player = null;
        
        var parts = msg.Split(' ');
        if (parts.Length < 2)
        {
            return GetString("JusticeCommandFormatError");
        }
        
        if (!byte.TryParse(parts[1], out var playerId))
        {
            return GetString("JusticeInvalidPlayerId");
        }
        
        player = Utils.GetPlayerById(playerId);

        return null;
    }
    private void CheckExecuteScale()
    {
        if (SelectedPlayers.Count != 2) return;
        Player.ShowPopUp(string.Format(GetString("JusticeScaleEstablished")));
        SkillLimits--;
        var player1 = Utils.GetPlayerById(SelectedPlayers[0]);
        var player2 = Utils.GetPlayerById(SelectedPlayers[1]);

        JusticeScalePlayer = Player.PlayerId;

        MeetingHud.Instance.RpcForceEndMeeting();
        _ = new LateTask(() =>
        {
        PlayerControl.LocalPlayer.NoCheckStartMeeting(null, true);
        
        _ = new LateTask(() =>
        {
            Utils.SendMessage(
                string.Format(GetString("JusticeScaleAnnouncement"), 
                    player1.GetRealName(), player2.GetRealName()),
                255,
                Utils.ColorString(Utils.GetRoleColor(CustomRoles.Justice), GetString("JusticeScaleTitle"))
            );
            foreach (var player in Main.AllPlayerControls)
            {
                player.KillFlash();
            }
        }, 0.5f, "Justice Scale Announcement");
        }, 2f, "Justice Scale Announcement");
    }
    
    public override bool OnSendMessage(string msg, out MsgRecallMode recallMode)
    {
        var isCommand = JusticeMsg(msg, out var spam);
        recallMode = spam ? MsgRecallMode.Spam : MsgRecallMode.None;
        return isCommand;
    }

    private bool JusticeMsg(string msg, out bool spam)
    {
        spam = false;
        if (!GameStates.IsInGame || Player == null) return false;
        if (!Player.Is(CustomRoles.Justice)) return false;

        int operate;
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (ChatCommand.MatchCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) 
            operate = 1;
        else if (ChatCommand.MatchCommand(ref msg, "scale|天平|审判|jtc|sc", false))
            operate = 2;
        else 
            return false;

        if (!Player.IsAlive())
        {
            Utils.SendMessage(GetString("JusticeDead"), Player.PlayerId);
            return true;
        }

        switch (operate)
        {
            case 1:
                Utils.SendMessage(ChatCommand.GetFormatString(false, true), Player.PlayerId);
                break;
            case 2:
            {
                spam = true;
                if (!AmongUsClient.Instance.AmHost) return true;

                if (SkillLimits <= 0)
                {
                    Utils.SendMessage(GetString("JusticeLimitMax"), Player.PlayerId);
                    return true;
                }

                if (SelectedPlayers.Count == 2)
                {
                    Utils.SendMessage(GetString("JusticeAlreadyExecuted"), Player.PlayerId);
                    return true;
                }

                var reason = MsgToPlayersByID(msg, out var player);
                if (!string.IsNullOrEmpty(reason))
                {
                    Utils.SendMessage(reason, Player.PlayerId);
                    return true;
                }

                if (!TrySelectPlayer(player, out var r))
                {
                    Utils.SendMessage(r);
                    return true;
                }

                CheckExecuteScale();
                break;
            }
        }

        return true;
    }

    

    public override void OnPlayerDeath(PlayerControl player, CustomDeathReason deathReason, bool isOnMeeting = false)
    {
        if (!isOnMeeting) return;

        if (player.PlayerId == JusticeScalePlayer)
        {
            JusticeScalePlayer = 255;
            SendRPC();
            return;
        }
        if (JusticeScalePlayer == 255)
        {
            SelectedPlayers.Remove(player.PlayerId);
            SendRPC();
            return;
        }
        
        if (!SelectedPlayers.Contains(player.PlayerId)) return;
        
        var survivorId = SelectedPlayers.Find(x => x != player.PlayerId);
        MeetingVoteManager.Instance.ClearAndExile(player.PlayerId,survivorId);
        Utils.SendMessage(
            string.Format(GetString("JusticeScaleDeathResult"), 
                player.GetRealName(),
                Utils.GetPlayerById(survivorId).GetRealName()),
            255,
            Utils.ColorString(Utils.GetRoleColor(CustomRoles.Justice), GetString("JusticeScaleTitle"))
        );
    }


}