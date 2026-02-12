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
        CanUseTimes = OptionCanUseTimes.GetInt();
        SelectedPlayers = new List<byte>();
        CustomRoleManager.CheckVote.Add(CheckVoteOthers);
    }

    private static OptionItem OptionCanUseTimes;
    private static OptionItem OptionScaleMeetingTime;
    enum OptionName
    {
        JusticeCanUseTimes,
        JusticeScaleMeetingTime,
    }

    public int CanUseTimes = 0;
    public List<byte> SelectedPlayers = new();
    private bool HasExecutedThisMeeting = false;
    private int CurrentUses = 0;
    public static bool JusticeScaleActive = false;
    public static List<byte> JusticeScaleTargets = new();

    private enum RoleRpcType
    {
        SetSelectedPlayers,
        SetCurrentUses,
        SetJusticeScaleActive
    }

    private static void SetupOptionItem()
    {
        OptionCanUseTimes = IntegerOptionItem.Create(RoleInfo, 11, OptionName.JusticeCanUseTimes, new(1, 15, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
        OptionScaleMeetingTime = IntegerOptionItem.Create(RoleInfo, 12, OptionName.JusticeScaleMeetingTime, new(15, 120, 5), 45, false)
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Add()
    {
        CanUseTimes = OptionCanUseTimes.GetInt();
        CurrentUses = CanUseTimes;
        SelectedPlayers.Clear();
    }

    public override void OnStartMeeting()
    {
        if (JusticeScaleActive) 
        {
            var player1 = Utils.GetPlayerById(JusticeScaleTargets[0]);
            var player2 = Utils.GetPlayerById(JusticeScaleTargets[1]);
            
            Utils.SendMessage(
                string.Format(GetString("JusticeSpecialMeeting"), 
                    player1.GetRealName(),
                    player2.GetRealName()),
                255,
                Utils.ColorString(Utils.GetRoleColor(CustomRoles.Justice), GetString("JusticeScaleTitle"))
            );
        }
        
        SelectedPlayers.Clear();
        HasExecutedThisMeeting = false;
        SendRPC();
        
        if (Player.IsAlive())
        {
            Utils.SendMessage(
                string.Format(GetString("JusticeUsesRemaining"), CurrentUses), 
                Player.PlayerId,
                Utils.ColorString(Utils.GetRoleColor(CustomRoles.Justice), GetString("JusticeScaleTitle"))
            );
        }
    }

    public override void AfterMeetingTasks()
    {
        ResetJusticeScale();
        SelectedPlayers.Clear();
        HasExecutedThisMeeting = false;
    }

    public override void OverrideNameAsSeer(PlayerControl seen, ref string nameText, bool isForMeeting = false)
    {
        if (Player.IsAlive() && isForMeeting)
        {
            nameText = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Justice), seen.PlayerId.ToString()) + " " + nameText;
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (JusticeScaleActive && MeetingHud.Instance != null)
        {
            foreach (var targetId in JusticeScaleTargets)
            {
                CheckJusticeScaleDeath(targetId);
            }
        }
    }

    public static bool CheckVoteOthers(PlayerControl voter, PlayerControl voted)
    {
        if (voter.IsAlive() && JusticeScaleActive && voted != null)
        {
            if (voted.PlayerId != 253 && !JusticeScaleTargets.Contains(voted.PlayerId))
            {
                Utils.SendMessage(GetString("JusticeVoteInvalid"), voter.PlayerId);
                return false;
            }
        }
        return true;
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write((byte)RoleRpcType.SetSelectedPlayers);
        sender.Writer.Write(SelectedPlayers.Count);
        foreach (var playerId in SelectedPlayers)
            sender.Writer.Write(playerId);
        sender.Writer.Write(CurrentUses);
        sender.Writer.Write(JusticeScaleActive);
        sender.Writer.Write(JusticeScaleTargets.Count);
        foreach (var targetId in JusticeScaleTargets)
            sender.Writer.Write(targetId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        var rpcType = (RoleRpcType)reader.ReadByte();
        switch (rpcType)
        {
            case RoleRpcType.SetSelectedPlayers:
                SelectedPlayers.Clear();
                var count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                    SelectedPlayers.Add(reader.ReadByte());
                CurrentUses = reader.ReadInt32();
                JusticeScaleActive = reader.ReadBoolean();
                JusticeScaleTargets.Clear();
                var targetCount = reader.ReadInt32();
                for (int i = 0; i < targetCount; i++)
                    JusticeScaleTargets.Add(reader.ReadByte());
                break;
        }
    }
    
    public string ButtonName { get; private set; } = "Scale";
    public bool ShouldShowButton() => Player.IsAlive() && !HasExecutedThisMeeting && CurrentUses > 0;
    public bool ShouldShowButtonFor(PlayerControl target) => !HasExecutedThisMeeting && CurrentUses > 0 && target.IsAlive();

    public void OnClickButton(PlayerControl target)
    {
        if (!TrySelectPlayer(target, out string reason))
        {
            Player.ShowPopUp(reason);
            return;
        }
        
        if (SelectedPlayers.Count == 2)
        {
            ExecuteScale();
            HasExecutedThisMeeting = true;
            CurrentUses--;
            SendRPC();
        }
    }

    public void OnUpdateButton(MeetingHud meetingHud)
    {
        foreach (var pva in meetingHud.playerStates)
        {
            var btn = pva?.transform?.FindChild("Custom Meeting Button")?.gameObject;
            if (!btn) continue;
            
            if (SelectedPlayers.Contains(pva.TargetPlayerId))
                btn.GetComponent<SpriteRenderer>().color = Color.yellow;
            else if (SelectedPlayers.Count == 2 || HasExecutedThisMeeting || CurrentUses <= 0)
                btn.GetComponent<SpriteRenderer>().color = Color.gray;
            else
                btn.GetComponent<SpriteRenderer>().color = Color.white;
        }
    }
    
    private bool TrySelectPlayer(PlayerControl target, out string reason)
    {
        reason = string.Empty;

        if (HasExecutedThisMeeting)
        {
            reason = GetString("JusticeAlreadyExecuted");
            return false;
        }

        if (CurrentUses <= 0)
        {
            reason = GetString("JusticeLimitMax");
            return false;
        }

        if (SelectedPlayers.Count >= 2)
        {
            reason = GetString("JusticeAlreadySelectedTwo");
            return false;
        }
        
        if (SelectedPlayers.Contains(target.PlayerId))
        {
            reason = GetString("JusticePlayerAlreadySelected");
            return false;
        }

        SelectedPlayers.Add(target.PlayerId);
        SendRPC();
        
        if (SelectedPlayers.Count == 1)
        {
            Player.ShowPopUp(string.Format(GetString("JusticeFirstSelected"), target.GetRealName()));
        }
        else
        {
            Player.ShowPopUp(string.Format(GetString("JusticeScaleEstablished"), 
                Utils.GetPlayerById(SelectedPlayers[0]).GetRealName(), 
                target.GetRealName()));
        }
        
        return true;
    }
    
    private void ExecuteScale()
    {
        var player1 = Utils.GetPlayerById(SelectedPlayers[0]);
        var player2 = Utils.GetPlayerById(SelectedPlayers[1]);
        
        SetJusticeScaleTargets(SelectedPlayers[0], SelectedPlayers[1]);
        MeetingVoteManager.Instance?.ClearVotes();
        MeetingTimeManager.Init();
        _ = new LateTask(() =>
        {
            Utils.SendMessage(
                string.Format(GetString("JusticeScaleAnnouncement"), 
                    player1.GetRealName(), player2.GetRealName()),
                255,
                Utils.ColorString(Utils.GetRoleColor(CustomRoles.Justice), GetString("JusticeScaleTitle"))
            );
        }, 0.5f, "Justice Scale Announcement");
    }
    
    public override bool OnSendMessage(string msg, out MsgRecallMode recallMode)
    {
        bool isCommand = JusticeMsg(Player, msg, out bool spam);
        recallMode = spam ? MsgRecallMode.Spam : MsgRecallMode.None;
        return isCommand;
    }

    private bool JusticeMsg(PlayerControl pc, string msg, out bool spam)
    {
        spam = false;
        if (!GameStates.IsInGame || pc == null) return false;
        if (!pc.Is(CustomRoles.Justice)) return false;

        int operate;
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (ChatCommand.MatchCommand(ref msg, "scale|天平|审判|jtc", false))
            operate = 1;
        else 
            return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("JusticeDead"), pc.PlayerId);
            return true;
        }

        if (operate == 1)
        {
            spam = true;
            if (!AmongUsClient.Instance.AmHost) return true;

            if (CurrentUses <= 0)
            {
                Utils.SendMessage(GetString("JusticeLimitMax"), pc.PlayerId);
                return true;
            }

            if (HasExecutedThisMeeting)
            {
                Utils.SendMessage(GetString("JusticeAlreadyExecuted"), pc.PlayerId);
                return true;
            }

            if (!MsgToPlayersByID(msg, out PlayerControl player1, out PlayerControl player2, out string error))
            {
                Utils.SendMessage(error, pc.PlayerId);
                return true;
            }

            if (!CheckAble(player1, player2))
                return true;

            SelectedPlayers.Clear();
            SelectedPlayers.Add(player1.PlayerId);
            SelectedPlayers.Add(player2.PlayerId);
            
            ExecuteScale();
            HasExecutedThisMeeting = true;
            CurrentUses--;
            SendRPC();
        }
        return true;
    }
    
    private static bool MsgToPlayersByID(string msg, out PlayerControl player1, out PlayerControl player2, out string error)
    {
        player1 = null;
        player2 = null;
        error = string.Empty;
        
        string[] parts = msg.Split(' ');
        if (parts.Length < 3)
        {
            error = GetString("JusticeCommandFormatError");
            return false;
        }
        
        if (!byte.TryParse(parts[1], out byte player1Id))
        {
            error = GetString("JusticeInvalidPlayerId");
            return false;
        }
        
        if (!byte.TryParse(parts[2], out byte player2Id))
        {
            error = GetString("JusticeInvalidPlayerId");
            return false;
        }
        
        player1 = Utils.GetPlayerById(player1Id);
        player2 = Utils.GetPlayerById(player2Id);

        if (player1 == null || player2 == null)
        {
            error = GetString("JusticePlayerNotFound");
            return false;
        }

        return true;
    }

    private bool CheckAble(PlayerControl player1, PlayerControl player2)
    {
        if (player1.PlayerId == player2.PlayerId)
        {
            Utils.SendMessage(GetString("JusticeSamePlayer"), Player.PlayerId);
            return false;
        }

        if (!player1.IsAlive())
        {
            Utils.SendMessage(GetString("JusticeTargetDead"), Player.PlayerId);
            return false;
        }

        if (!player2.IsAlive())
        {
            Utils.SendMessage(GetString("JusticeTargetDead"), Player.PlayerId);
            return false;
        }

        return true;
    }
    
    public static void SetJusticeScaleTargets(byte target1, byte target2)
    {
        JusticeScaleActive = true;
        JusticeScaleTargets.Clear();
        JusticeScaleTargets.Add(target1);
        JusticeScaleTargets.Add(target2);
    }
    
    public static void ResetJusticeScale()
    {
        JusticeScaleActive = false;
        JusticeScaleTargets.Clear();
    }
    
    public static void CheckJusticeScaleDeath(byte deadPlayerId)
    {
        if (JusticeScaleActive && JusticeScaleTargets.Contains(deadPlayerId) && MeetingHud.Instance != null)
        {
            var survivorId = JusticeScaleTargets.Find(x => x != deadPlayerId);
            if (survivorId != 253)
            {
                var survivor = Utils.GetPlayerById(survivorId);
                if (survivor != null && survivor.IsAlive())
                {
                    MeetingHud.Instance.RpcVotingComplete(
                        new MeetingHud.VoterState[0], 
                        survivor.Data, 
                        false
                    );
                    Utils.SendMessage(
                        string.Format(GetString("JusticeScaleDeathResult"), 
                            Utils.GetPlayerById(deadPlayerId).GetRealName(),
                            survivor.GetRealName()),
                        255,
                        Utils.ColorString(Utils.GetRoleColor(CustomRoles.Justice), GetString("JusticeScaleTitle"))
                    );
                }
            }
            ResetJusticeScale();
        }
    }
}