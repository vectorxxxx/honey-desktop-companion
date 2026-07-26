using Honey.Domain.Activity;
using Honey.Domain.Model;

namespace Honey.Desktop.Status;

public static class PetStatusText
{
    public static string Mood(PetMood mood) => mood switch
    {
        PetMood.Curious => "好奇",
        PetMood.Happy => "愉悦",
        PetMood.Sleepy => "困倦",
        PetMood.Angry => "警戒",
        PetMood.Hungry => "饥饿",
        PetMood.Hurt => "受伤",
        PetMood.Alert => "警觉",
        _ => mood.ToString()
    };

    public static string Behavior(string behavior) => behavior switch
    {
        "observe" => "观察",
        "play" => "玩耍",
        "sleep" => "休眠",
        "forage" => "觅食",
        "web" => "结网",
        "pounce" => "扑跃",
        "groom" => "梳理",
        "pet" => "接受抚摸",
        "mode" => "形态切换",
        _ => behavior
    };

    public static string Phase(string phase)
    {
        if (string.IsNullOrWhiteSpace(phase))
        {
            return "感知环境";
        }

        var key = phase.Contains('.', StringComparison.Ordinal)
            ? phase[(phase.LastIndexOf('.') + 1)..]
            : phase;
        return key switch
        {
            "turn" => "转向",
            "track" => "追踪",
            "bounce" => "弹跳",
            "chase" => "追逐",
            "curl" => "蜷伏",
            "breathe" => "吐纳",
            "discover" => "发现",
            "approach" => "靠近",
            "capture" => "捕获",
            "eat" => "进食",
            "anchor" => "定锚",
            "silk" => "吐丝",
            "weave" => "织网",
            "rest" => "休整",
            "charge" => "蓄势",
            "leap" => "跃击",
            "retreat" => "回撤",
            "start" => "起势",
            "alternate" => "交替梳理",
            "finish" => "收势",
            _ => phase
        };
    }

    public static string Origin(BehaviorOrigin origin) => origin switch
    {
        BehaviorOrigin.LocalAutonomy => "本地自主",
        BehaviorOrigin.AiSuggestion => "AI 建议",
        BehaviorOrigin.UserInteraction => "用户互动",
        BehaviorOrigin.SystemSchedule => "系统调度",
        _ => origin.ToString()
    };

    public static string OriginIconKey(BehaviorOrigin origin) => origin switch
    {
        BehaviorOrigin.LocalAutonomy => "IconCompass",
        BehaviorOrigin.AiSuggestion => "IconBot",
        BehaviorOrigin.UserInteraction => "IconUserRound",
        BehaviorOrigin.SystemSchedule => "IconClock",
        _ => "IconActivity"
    };

    public static string Duration(TimeSpan duration) =>
        duration.TotalMinutes >= 1
            ? $"{(int)duration.TotalMinutes}分 {duration.Seconds:00}秒"
            : $"{Math.Max(0, (int)duration.TotalSeconds)}秒";

    public static string Activity(PetActivityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var outcome = entry.Outcome switch
        {
            PetActivityOutcome.Started => "开始",
            PetActivityOutcome.Completed => "完成",
            PetActivityOutcome.Rejected => "未执行",
            PetActivityOutcome.Interrupted => "中断",
            _ => entry.Outcome.ToString()
        };
        var detail = string.IsNullOrWhiteSpace(entry.Detail)
            ? string.Empty
            : $" · {entry.Detail}";
        return $"{entry.At:HH:mm:ss}  {Origin(entry.Origin)} · {Behavior(entry.Behavior.Value)} · {outcome}{detail}";
    }
}
