using System;
using System.Collections.Generic;
using CoffeeGame.Domain;
using CoffeeGame.Run;
using UnityEngine;
using UnityEngine.UI;

namespace CoffeeGame.UI
{
    public sealed partial class CombatGameHudView
    {
        private RectTransform partyPanel;
        private Text partyNotice;
        private readonly Dictionary<string, Text> partyLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, Button> partySwitches = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> partyRestButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Text> partyMenuLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, Button> partyMenuButtons = new Dictionary<string, Button>();
        private CombatRunController renderedPartyRun;
        private RawImage activeCatPortrait;
        private Texture2D dragonPartyPortrait, catPartyPortrait;
        private int selectedPartyMember;

        public void NavigateParty(int direction, bool confirm)
        {
            if (renderedPartyRun?.Party == null) return;
            var members = renderedPartyRun.Party.State.Members;
            selectedPartyMember = Mathf.Clamp(selectedPartyMember + direction, 0, members.Count - 1);
            if (confirm && members.Count > 0) renderedPartyRun.Party.ToggleParticipation(members[selectedPartyMember].Id);
        }

        private void BuildPartyHud(Transform parent)
        {
            partyPanel = CreateRect("Party", parent, Vector2.zero, Vector2.one);
            partyNotice = CreateText("Party Notice", partyPanel, 21, FontStyle.Bold, TextAnchor.UpperLeft, Accent);
            SetTopLeft(partyNotice.rectTransform, new Vector2(28, -434), new Vector2(620, 65));
        }

        private void RefreshParty(CombatRunController run, bool pauseMenuOpen)
        {
            renderedPartyRun = run;
            if (partyPanel == null || run.Party == null) return;
            partyPanel.gameObject.SetActive(!pauseMenuOpen && run.Mode != CombatRunMode.RivalEncounter);
            int index = 0;
            foreach (var member in run.Party.State.Members)
            {
                if (!partyLabels.TryGetValue(member.Id, out var label))
                {
                    string id = member.Id;
                    var panel = CreateImage("Party " + id, partyPanel, Panel);
                    SetTopLeft(panel.rectTransform, new Vector2(28, -236 - index * 92), new Vector2(560, 84));
                    label = CreateText("Resources", panel.transform, 19, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
                    SetTopLeft(label.rectTransform, new Vector2(12, -4), new Vector2(300, 76));
                    partyLabels.Add(id, label);
                    var switchButton = CreateButton("Switch", panel.transform, "操作", 20, () => renderedPartyRun?.Party?.RequestSwitch(id));
                    SetTopLeft(switchButton.GetComponent<RectTransform>(), new Vector2(323, -10), new Vector2(102, 64));
                    partySwitches.Add(id, switchButton);
                    var restButton = CreateButton("Participation", panel.transform, "休息", 20, () => renderedPartyRun?.Party?.ToggleParticipation(id));
                    SetTopLeft(restButton.GetComponent<RectTransform>(), new Vector2(437, -10), new Vector2(110, 64));
                    partyRestButtons.Add(id, restButton);
                }
                label.text = MemberSummary(member, run.Party.Active != null && run.Party.Active.MemberId == member.Id);
                bool touchHud = input != null && input.UsesTouchOverlay;
                var memberPanel = label.transform.parent.GetComponent<RectTransform>();
                memberPanel.localScale = Vector3.one * (touchHud ? .78f : 1f);
                SetTopLeft(memberPanel, new Vector2(28, touchHud ? -188-index*74 : -236-index*92),new Vector2(560,84));
                memberPanel.GetComponent<Image>().color = touchHud ? new Color(.025f,.043f,.066f,.48f) : Panel;
                bool available = member.RecoveryState != PartyRecoveryState.KnockedOut && !run.Party.TimeStopped;
                partySwitches[member.Id].interactable = available && member.RecoveryState == PartyRecoveryState.Deployed && run.Mode == CombatRunMode.Playing;
                var rest = partyRestButtons[member.Id];
                rest.interactable = available;
                rest.GetComponentInChildren<Text>().text = member.RecoveryState == PartyRecoveryState.Resting ? "出撃" : "休息";
                if (partyMenuLabels.TryGetValue(member.Id, out var menuLabel) && menuLabel != null)
                {
                    menuLabel.text = MemberSummary(member, run.Party.Active != null && run.Party.Active.MemberId == member.Id) +
                        $"\nLv.{member.Level}　力 {member.Status.Strength}　敏捷 {member.Status.Agility}　技量 {member.Status.Technique}\n運 {member.Status.Luck}　生命 {member.Status.Vitality}";
                    var button = partyMenuButtons[member.Id];
                    SetButtonColor(button, index == selectedPartyMember ? Selected : PanelLight);
                    button.interactable = available;
                    button.GetComponentInChildren<Text>().text = member.RecoveryState == PartyRecoveryState.Resting ? "この仲間を出撃させる" : "この仲間を休息させる";
                }
                index++;
            }
            partyNotice.text = run.Party.TimeStopped
                ? $"時を止める　{Combat.TimeStopController.Instance.Remaining:0.0} 秒\n停止中の命中は解除時に反映"
                : (input != null && input.UsesTouchOverlay ? "切替：操作キャラ変更　休息中はHP・MPが回復\n" : "T / RB：操作切替　休息中はHP・MPが回復\n") + run.Party.Notice;
            if (input != null && input.UsesTouchOverlay)
            {
                if (!run.Party.TimeStopped) partyNotice.text = run.Party.Notice;
                SetTopLeft(partyNotice.rectTransform,new Vector2(28,-188-index*74),new Vector2(520,65));
                partyNotice.fontSize = 18;
            }
            else { SetTopLeft(partyNotice.rectTransform,new Vector2(28,-250-index*92),new Vector2(620,65)); partyNotice.fontSize=21; }
            if (activeCatPortrait != null)
            {
                if (catPartyPortrait == null) catPartyPortrait = Resources.Load<Texture2D>(RivalPortraitResource);
                if (dragonPartyPortrait == null) dragonPartyPortrait = Resources.Load<Texture2D>(RivalPortraitCatalog.SplitInkResource);
                activeCatPortrait.texture = run.Party.Active?.IsDragon == true ? dragonPartyPortrait : catPartyPortrait;
                activeCatPortrait.gameObject.SetActive(run.Party.Active != null && (run.Party.Active.IsCat || run.Party.Active.IsDragon));
            }
            if (run.Party.Active?.IsDragon == true)
            {
                var combat = run.Party.Active.Combat;
                partyNotice.text = $"魔法：{combat.DragonActionLabel}" + (combat.DragonBreathRemaining > 0f ? $"　龍の呼吸 {combat.DragonBreathRemaining:0}秒" : "");
            }
        }

        private string MemberSummary(PartyMember member, bool active)
        {
            string name = member.Id == PartyMemberIds.Hero ? "主人公" : member.Id == PartyMemberIds.DragonGirl ? "龍少女" : "猫少女";
            string state = member.RecoveryState == PartyRecoveryState.KnockedOut
                ? $"戦闘不能 {FormatRecovery(member.KnockoutRemainingSeconds(renderedPartyRun.Party.UtcNow))}"
                : member.RecoveryState == PartyRecoveryState.Resting ? "休息中" : active ? "操作中" : "自動戦闘";
            double hp = member.Resources.HitPoints, mp = member.Resources.MagicPoints;
            if (member.RecoveryState == PartyRecoveryState.Deployed && renderedPartyRun.Party.Actors.TryGetValue(member.Id, out var actor))
            { hp = actor.Health.Current; mp = actor.Resources.MagicPoints; }
            return $"{name}　{state}\nHP {hp:0}/{member.Resources.MaximumHitPoints:0}　MP {mp:0}/{member.Resources.MaximumMagicPoints:0}";
        }

        private static string FormatRecovery(double seconds)
        {
            int remaining = Math.Max(0, (int)Math.Ceiling(seconds));
            return $"{remaining / 60:00}:{remaining % 60:00}";
        }

        private void BuildPartyMenu(CombatRunController run)
        {
            partyMenuLabels.Clear(); partyMenuButtons.Clear();
            AddSectionHeading(menuScrollContent, "戦闘に参加する仲間", 29, Accent, 48f);
            foreach (var member in run.Party.State.Members)
            {
                string id = member.Id;
                var label = CreateText("Party Member", menuScrollContent, 25, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
                label.gameObject.AddComponent<LayoutElement>().preferredHeight = 142f;
                partyMenuLabels.Add(id, label);
                var button = CreateButton("Participation", menuScrollContent, "休息／出撃", 24, () => run.Party.ToggleParticipation(id));
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
                partyMenuButtons.Add(id, button);
            }
            AddSectionHeading(menuScrollContent, "戦闘不能は10分でHP1%に復活。休息のHP・MPは約15分で全回復。終了中・ポーズ中も進みます。", 21, MutedInk, 86f);
        }
    }
}
