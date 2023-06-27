﻿using AmongUs.GameOptions;

using TOHE.Roles.Core;
using TOHE.Roles.Core.Interfaces;
using static TOHE.GuesserHelper;

namespace TOHE.Roles.Impostor;
public sealed class EvilGuesser : RoleBase, IImpostor, IMeetingButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(EvilGuesser),
            player => new EvilGuesser(player),
            CustomRoles.EvilGuesser,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            1000,
            SetupOptionItem,
            "eg|邪惡賭怪|邪恶的赌怪|坏赌|邪恶赌|恶赌|赌怪"
        );
    public EvilGuesser(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    { }

    public static OptionItem OptionGuessNums;
    public static OptionItem OptionCanGuessImp;
    public static OptionItem OptionCanGuessAddons;
    public static OptionItem OptionCanGuessVanilla;
    public static OptionItem OptionCanGuessTaskDoneSnitch;
    public static OptionItem OptionHideMsg;
    enum OptionName
    {
        GuesserCanGuessTimes,
        EGCanGuessImp,
        EGCanGuessAdt,
        EGCanGuessVanilla,
        EGCanGuessTaskDoneSnitch,
        EGGuesserTryHideMsg,
    }

    public int GuessLimit;
    private static void SetupOptionItem()
    {
        OptionGuessNums = IntegerOptionItem.Create(RoleInfo, 10, OptionName.GuesserCanGuessTimes, new(1, 15, 1), 15, false)
            .SetValueFormat(OptionFormat.Times);
        OptionCanGuessImp = BooleanOptionItem.Create(RoleInfo, 11, OptionName.EGCanGuessImp, true, false);
        OptionCanGuessAddons = BooleanOptionItem.Create(RoleInfo, 12, OptionName.EGCanGuessAdt, false, false);
        OptionCanGuessVanilla = BooleanOptionItem.Create(RoleInfo, 13, OptionName.EGCanGuessVanilla, true, false);
        OptionCanGuessTaskDoneSnitch = BooleanOptionItem.Create(RoleInfo, 14, OptionName.EGCanGuessTaskDoneSnitch, true, false);
        OptionHideMsg = BooleanOptionItem.Create(RoleInfo, 15, OptionName.EGGuesserTryHideMsg, true, false);
    }
    public override void Add()
    {
        GuessLimit = OptionGuessNums.GetInt();
    }
    public override void OverrideNameAsSeer(PlayerControl seen, ref string nameText, bool isForMeeting = false)
    {
        if (Player.IsAlive() && seen.IsAlive() && isForMeeting)
        {
            nameText = Utils.ColorString(Utils.GetRoleColor(CustomRoles.EvilGuesser), seen.PlayerId.ToString()) + " " + nameText;
        }
    }
    public string ButtonName { get; private set; } = "Target";
    public bool ShouldShowButton() => Player.IsAlive();
    public bool ShouldShowButtonFor(PlayerControl target) => target.IsAlive();
    public override bool OnSendMessage(string msg) => GuesserMsg(Player, msg);
    public bool OnClickButtonLocal(PlayerControl target)
    {
        ShowGuessPanel(target.PlayerId, MeetingHud.Instance);
        return false;
    }
}