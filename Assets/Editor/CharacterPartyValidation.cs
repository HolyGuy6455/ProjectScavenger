using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Checks party replacement without a network session or saved scene changes.</summary>
public static class CharacterPartyValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Characters/Validate Party Death and Switching (Edit Mode)")]
    private static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Run party checks in Edit Mode, outside a network session.");

        var results = new List<string>();
        CheckStoredHealth(results);
        CheckDamageReplacement(results);
        CheckSetHealthReplacement(results);
        CheckDeathAnimations(results);
        CheckGameOverWait(results);
        CheckPlayerIsolation(results);
        CheckPresentationInterruption(results);
        CheckDeadRegeneration(results);
        CheckSimultaneousStarvation(results);
        CheckServerSurvivalRecovery(results);
        CheckServerSurvivalIsolation(results);
        CheckHighFrameRateSurvival(results);
        CheckCharacterStateSerialization(results);
        CheckCharacterStateIdentity(results);
        return "[CharacterPartyValidation]\n" + string.Join("\n", results);
    }

    private static void CheckStoredHealth(List<string> results)
    {
        using (var fixture = new Fixture())
        {
            fixture.Health.SetHealth(45f, 100f);
            Check(fixture.Player.TryChangeCharacter(2), "living second character can be selected");
            Check(fixture.Player.ActiveSlotIndex == 1 && Near(fixture.Health.CurrentHealth, 100f),
                "first selection restores the second character's own health");
            fixture.Health.SetHealth(23f, 100f);
            Check(fixture.Player.TryChangeCharacter(1) && Near(fixture.Health.CurrentHealth, 45f),
                "returning to the first character preserves its damage");
            Check(fixture.Player.TryChangeCharacter(2) && Near(fixture.Health.CurrentHealth, 23f),
                "returning to the second character preserves its damage");
            Check(!fixture.Player.TryChangeCharacter(999) && fixture.Player.ActiveSlotIndex == 1,
                "a character outside the party cannot be selected");
            Check(!fixture.Player.TrySwitchToLivingCharacter() && fixture.Player.ActiveSlotIndex == 1,
                "automatic replacement cannot skip a living active character");
        }
        results.Add("PASS manual switching preserves each character's remaining health and rejects non-party IDs");
    }

    private static void CheckDamageReplacement(List<string> results)
    {
        using (var fixture = new Fixture())
        {
            fixture.SetHealth(2, 37f, 100f);
            fixture.Health.TakeDamage(new DamageInfo(150f));
            CheckDeathPending(fixture, 0);
            Check(!fixture.Player.TryChangeCharacter(2) && !RequestServerSlot(fixture.Player, 1),
                "manual switching cannot interrupt death playback");
            fixture.Health.TakeDamage(new DamageInfo(100f));
            fixture.Health.Heal(100f);
            Check(fixture.DeathEvents == 1 && Near(fixture.Health.CurrentHealth, 0f),
                "damage and healing cannot interrupt or repeat a character's death");
            CompleteDeath(fixture.Player);
            Check(fixture.Player.ActiveSlotIndex == 1 && Near(fixture.Health.CurrentHealth, 37f),
                "death completion selects the next living character with its saved health");
            Check(!fixture.Health.IsDead && !fixture.Player.IsDeathPresented && !fixture.Player.IsInputLocked
                && fixture.DeathEvents == 1, "the replacement character clears death and input locks");
            Check(!fixture.Player.IsCharacterAlive(1) && !fixture.Player.TryChangeCharacter(1)
                && fixture.Player.ActiveSlotIndex == 1 && Near(fixture.Health.CurrentHealth, 37f),
                "dead-character requests leave both selection and health unchanged");
            Check(!RequestServerSlot(fixture.Player, 0) && fixture.Player.ActiveSlotIndex == 1,
                "the authoritative slot handler rejects a dead target independently of the request guard");
            AdvanceSurvival(fixture.Player, 1f);
            Check(!fixture.Player.IsCharacterAlive(1), "survival updates cannot revive a dead reserve");

            fixture.SetHealth(3, 0f, 100f);
            fixture.Health.TakeDamage(new DamageInfo(100f));
            CheckDeathPending(fixture, 1);
            CompleteDeath(fixture.Player);
            Check(fixture.Player.ActiveSlotIndex == 3 && Near(fixture.Health.CurrentHealth, 100f),
                "automatic replacement skips an already dead reserve");
            Check(!fixture.Player.IsCharacterAlive(2) && !fixture.Player.IsCharacterAlive(3)
                && fixture.DeathEvents == 2, "each fallen character reports death while dead reserves stay dead");

            fixture.Health.TakeDamage(new DamageInfo(100f));
            CheckDeathPending(fixture, 3);
            CompleteDeath(fixture.Player);
            Check(fixture.Health.IsDead && fixture.Player.IsDeathPresented && fixture.Player.IsInputLocked
                && !fixture.Player.IsWaitingForDeathAnimation && fixture.DeathEvents == 3,
                "exhausting the party retains final death after playback completes");
            fixture.Health.TakeDamage(new DamageInfo(100f));
            CompleteDeath(fixture.Player);
            Check(fixture.DeathEvents == 3 && !fixture.Player.TryChangeCharacter(1),
                "damage and switch requests cannot revive or repeat final death");
        }
        results.Add("PASS each death locks input before replacement, skips dead reserves, and keeps an exhausted party dead");
    }

    private static void CheckSetHealthReplacement(List<string> results)
    {
        using (var fixture = new Fixture(new PlayerController.CharacterDeckData { char1 = 1, char3 = 3 }))
        {
            Check(!fixture.Player.TryChangeCharacter(0) && !fixture.Player.TryChangeCharacter(2),
                "empty slots and absent characters cannot be selected");
            Check(!RequestServerSlot(fixture.Player, 1) && !RequestServerSlot(fixture.Player, -1)
                && !RequestServerSlot(fixture.Player, 4), "the authoritative slot handler rejects empty and invalid slots");
            fixture.SetHealth(3, 19f, 100f);
            fixture.Health.SetHealth(0f, 100f);
            CheckDeathPending(fixture, 0);
            CompleteDeath(fixture.Player);
            Check(fixture.Player.ActiveSlotIndex == 2 && Near(fixture.Health.CurrentHealth, 19f)
                && !fixture.Health.IsDead && fixture.DeathEvents == 1,
                "zero health from survival updates skips empty slots after death completion");
            fixture.Health.SetHealth(0f, 100f);
            CheckDeathPending(fixture, 2);
            CompleteDeath(fixture.Player);
            Check(fixture.Health.IsDead && fixture.DeathEvents == 2,
                "zero health from survival updates ends an exhausted party after playback");
        }
        results.Add("PASS SetHealth(0) follows the same replacement and final-death policy, including sparse decks");
    }

    private static void CheckDeathAnimations(List<string> results)
    {
        for (int id = 1; id <= 4; id++)
        {
            int nextId = id % 4 + 1;
            using (var fixture = new Fixture(new PlayerController.CharacterDeckData { char1 = id, char2 = nextId }))
            {
                var prefab = Resources.Load<GameObject>($"Prefabs/Characters/char{id:D3}");
                Check(prefab != null, $"character {id} has a model prefab");
                var model = Object.Instantiate(prefab, fixture.Root.transform);
                var animator = model.GetComponentInChildren<Animator>(true);
                var presenter = CharacterAnimationPresenter.GetOrCreate(model);
                Check(animator != null && presenter != null, $"character {id} has an animation presenter");
                // Edit Mode requires explicit Animator evaluation and presenter initialization.
                Set(presenter, "animator", animator);
                animator.fireEvents = false;
                animator.Rebind();
                animator.Update(0f);
                var models = (GameObject[])typeof(PlayerController).GetField("_characterModels", Private).GetValue(fixture.Player);
                models[0] = model;

                fixture.Health.TakeDamage(new DamageInfo(100f));
                CheckDeathPending(fixture, 0);
                animator.Update(0f);
                var death = animator.GetCurrentAnimatorStateInfo(0);
                Check(death.IsName("Death") && death.length > 0f, $"character {id} enters its actual Death animation");
                CompleteDeath(fixture.Player);
                CheckDeathPending(fixture, 0);
                animator.Update(death.length * .5f);
                CompleteDeath(fixture.Player);
                CheckDeathPending(fixture, 0);
                animator.Update(death.length * .6f);
                CompleteDeath(fixture.Player);
                Check(fixture.Player.ActiveSlotIndex == 1 && !fixture.Health.IsDead && !fixture.Player.IsInputLocked
                    && !fixture.Player.IsWaitingForDeathAnimation && !fixture.Player.IsCharacterAlive(id),
                    $"character {id} switches only after its Death animation finishes");
            }
        }
        results.Add("PASS all four character Death animations retain the dying model until their actual clip completes");
    }

    private static void CheckGameOverWait(List<string> results)
    {
        using (var fixture = new Fixture(new PlayerController.CharacterDeckData { char1 = 1 }))
        {
            var local = typeof(PlayerController).GetProperty("LocalInstance").GetSetMethod(true);
            var previous = PlayerController.LocalInstance;
            var flowRoot = new GameObject("Death game-over validation") { hideFlags = HideFlags.HideAndDontSave };
            flowRoot.SetActive(false);
            try
            {
                local.Invoke(null, new object[] { fixture.Player });
                var flow = flowRoot.AddComponent<Scavenger.Manager.GameFlowManager>();
                fixture.Health.TakeDamage(new DamageInfo(100f));
                CheckDeathPending(fixture, 0);
                Check(flow.GetAlivePlayersCount() == 1, "game-over evaluation waits for the last Death animation");
                CompleteDeath(fixture.Player);
                Check(flow.GetAlivePlayersCount() == 0, "game-over evaluation sees the exhausted party after playback");
            }
            finally
            {
                local.Invoke(null, new object[] { previous });
                Object.DestroyImmediate(flowRoot);
            }
        }
        results.Add("PASS game-over evaluation waits until the last character's death presentation completes");
    }

    private static void CheckPlayerIsolation(List<string> results)
    {
        using (var first = new Fixture())
        using (var second = new Fixture())
        {
            first.Health.TakeDamage(new DamageInfo(100f));
            CompleteDeath(first.Player);
            Check(first.Player.ActiveSlotIndex == 1 && !first.Player.IsCharacterAlive(1),
                "the damaged server-side player advances its own party");
            Check(second.Player.ActiveSlotIndex == 0 && second.Player.IsCharacterAlive(1)
                && Near(second.Health.CurrentHealth, 100f) && second.DeathEvents == 0,
                "another player's party is unaffected by the first player's death");
            second.Health.SetHealth(64f, 100f);
            Check(second.Player.TryChangeCharacter(2) && second.Player.TryChangeCharacter(1)
                && Near(second.Health.CurrentHealth, 64f), "another player's switch uses its own saved health");
            Check(first.Player.ActiveSlotIndex == 1 && !first.Player.IsCharacterAlive(1),
                "another player's switch cannot revive or select the first player's dead character");
        }
        results.Add("PASS separate server-side players retain independent party health and selection");
    }

    private static void CheckPresentationInterruption(List<string> results)
    {
        using (var fixture = new Fixture())
        {
            int cancelled = 0;
            Set(fixture.Player, "_isCinematicPlaying", true);
            Set(fixture.Player, "_cinematicOnCancelled", (Action)(() => cancelled++));
            Check(!fixture.Player.TryChangeCharacter(2), "manual switching respects the current presentation lock");
            fixture.Health.TakeDamage(new DamageInfo(100f));
            CheckDeathPending(fixture, 0);
            Check(cancelled == 1, "death cancels the old presentation before playing Death");
            CompleteDeath(fixture.Player);
            Check(fixture.Player.ActiveSlotIndex == 1 && !fixture.Player.IsInputLocked
                && fixture.DeathEvents == 1, "death completion unlocks the replacement character");
        }
        results.Add("PASS lethal replacement cancels a presentation even when manual switching is locked");
    }

    private static void CheckDeadRegeneration(List<string> results)
    {
        var dead = new CharacterState(new CharData { id = 1 }) { currentHealth = 0f };
        dead.UpdateSurvivalStats(10f);
        Check(Near(dead.currentHealth, 0f), "a dead character cannot regenerate from hunger");
        var living = new CharacterState(new CharData { id = 2 }) { currentHealth = 50f };
        living.UpdateSurvivalStats(1f);
        Check(living.currentHealth > 50f, "living reserve regeneration is preserved");
        results.Add("PASS dead characters remain dead while living characters retain natural recovery");
    }

    private static void CheckSimultaneousStarvation(List<string> results)
    {
        using (var fixture = new Fixture())
        {
            fixture.SetHealth(1, .5f, 100f);
            fixture.SetHealth(2, .25f, 100f);
            fixture.Player.ModifyCharacterSurvivalStats(1, -100f, 0f);
            fixture.Player.ModifyCharacterSurvivalStats(2, -100f, 0f);
            AdvanceSurvival(fixture.Player, 1f);
            Check(!fixture.Player.IsCharacterAlive(1) && !fixture.Player.IsCharacterAlive(2),
                "the active character and starving reserve both reach zero in the same server tick");
            CheckDeathPending(fixture, 0);
            CompleteDeath(fixture.Player);
            Check(fixture.Player.ActiveSlotIndex == 2 && Near(fixture.Health.CurrentHealth, 100f)
                && !fixture.Health.IsDead && fixture.DeathEvents == 1,
                "replacement skips the reserve that dies during the same survival update");
        }
        using (var fixture = new Fixture())
        {
            for (int id = 1; id <= 4; id++)
            {
                fixture.SetHealth(id, .5f, 100f);
                fixture.Player.ModifyCharacterSurvivalStats(id, -100f, 0f);
            }
            AdvanceSurvival(fixture.Player, 1f);
            CheckDeathPending(fixture, 0);
            CompleteDeath(fixture.Player);
            Check(fixture.Health.IsDead && !fixture.Player.IsWaitingForDeathAnimation && fixture.DeathEvents == 1,
                "simultaneous starvation of the whole party reports final death once after playback");
            AdvanceSurvival(fixture.Player, 1f);
            Check(fixture.DeathEvents == 1, "subsequent survival updates cannot repeat final death");
        }
        results.Add("PASS server survival resolves all starving reserves before choosing a replacement or final death");
    }

    private static void CheckServerSurvivalRecovery(List<string> results)
    {
        using (var fixture = new Fixture())
        {
            fixture.SetHealth(1, 50f, 100f);
            fixture.SetHealth(2, 25f, 100f);
            fixture.SetHealth(3, 0f, 100f);
            AdvanceSurvival(fixture.Player, 1f);
            Check(fixture.Health.CurrentHealth > 50f && fixture.Player.IsCharacterAlive(2)
                && !fixture.Player.IsCharacterAlive(3),
                "server survival regenerates living active characters while preserving dead reserves");
            Check(fixture.Player.TryChangeCharacter(2) && fixture.Health.CurrentHealth > 25f,
                "switching restores a reserve's authoritative regenerated health");
            Check(!fixture.Player.TryChangeCharacter(3), "server survival cannot make a dead reserve selectable");

            fixture.Health.GrantClearProtection();
            float clearHealth = fixture.Health.CurrentHealth;
            fixture.Player.ModifyCharacterSurvivalStats(2, -100f, 0f);
            AdvanceSurvival(fixture.Player, 1f);
            Check(Near(fixture.Health.CurrentHealth, clearHealth) && !fixture.Health.IsDead,
                "server survival preserves clear protection");
        }
        results.Add("PASS authoritative survival preserves living reserve recovery, dead slots, and clear protection");
    }

    private static void CheckServerSurvivalIsolation(List<string> results)
    {
        using (var first = new Fixture())
        using (var second = new Fixture())
        {
            first.SetHealth(1, .5f, 100f);
            first.Player.ModifyCharacterSurvivalStats(1, -100f, 0f);
            second.SetHealth(1, 64f, 100f);
            AdvanceSurvival(first.Player, 1f);
            CompleteDeath(first.Player);
            Check(first.Player.ActiveSlotIndex == 1 && !first.Player.IsCharacterAlive(1)
                && second.Player.ActiveSlotIndex == 0 && Near(second.Health.CurrentHealth, 64f),
                "a server-owned remote player's starvation cannot affect another player's party");
            AdvanceSurvival(second.Player, 1f);
            Check(second.Health.CurrentHealth > 64f && second.Player.ActiveSlotIndex == 0
                && first.Player.ActiveSlotIndex == 1 && !first.Player.IsCharacterAlive(1),
                "remote server survival advances independently without the local state-manager singleton");
        }
        results.Add("PASS server survival advances each player's party independently, including non-owner fixtures");
    }

    private static bool Near(float actual, float expected) => Mathf.Abs(actual - expected) < .001f;

    private static void CheckHighFrameRateSurvival(List<string> results)
    {
        using (var fixture = new Fixture())
        {
            for (int frame = 0; frame < 144; frame++) AdvanceSurvival(fixture.Player, 1f / 144f);
            var active = fixture.Player.GetCharacterState(1);
            var reserve = fixture.Player.GetCharacterState(2);
            Check(active.currentHunger < 99.91f && active.currentThirst < 99.91f
                && reserve.currentHunger < 99.91f && reserve.currentThirst < 99.91f,
                "small per-frame hunger/thirst changes accumulate for active and reserve characters at 144 FPS");
        }
        results.Add("PASS server survival retains small per-frame changes at high frame rates");
    }

    private static void CheckCharacterStateSerialization(List<string> results)
    {
        var source = new CharacterState(new CharData { id = 2 })
        {
            currentHealth = 37f,
            maxHealth = 120f,
            currentHunger = 63f,
            maxHunger = 110f,
            currentThirst = 42f,
            maxThirst = 90f
        };
        CharacterState received;
        using (var writer = new FastBufferWriter(128, Allocator.Temp))
        {
            writer.WriteNetworkSerializable(source);
            using (var reader = new FastBufferReader(writer, Allocator.Temp))
                reader.ReadNetworkSerializable(out received);
        }
        Check(received.charId == source.charId && Near(received.currentHealth, source.currentHealth)
            && Near(received.maxHealth, source.maxHealth) && Near(received.currentHunger, source.currentHunger)
            && Near(received.maxHunger, source.maxHunger) && Near(received.currentThirst, source.currentThirst)
            && Near(received.maxThirst, source.maxThirst), "character vitals survive network serialization");

        var local = new CharacterState(new CharData { id = 2 });
        var skill = new SkillInstance(0) { currentCooldown = 7f };
        local.normalSkills[0] = skill;
        local.savedChamber.Add(new AmmoGroup { ammoId = 3, count = 11 });
        var skills = local.normalSkills;
        var chamber = local.savedChamber;
        local.CopyVitalsFrom(received);
        Check(Near(local.currentHealth, 37f) && Near(local.maxHealth, 120f)
            && Near(local.currentHunger, 63f) && Near(local.maxHunger, 110f)
            && Near(local.currentThirst, 42f) && Near(local.maxThirst, 90f),
            "receiving character vitals updates the existing local state");
        Check(ReferenceEquals(local.normalSkills, skills) && ReferenceEquals(local.normalSkills[0], skill)
            && Near(skill.currentCooldown, 7f) && ReferenceEquals(local.savedChamber, chamber)
            && local.savedChamber.Count == 1 && local.savedChamber[0].ammoId == 3
            && local.savedChamber[0].count == 11,
            "network vitals preserve local skill cooldowns and stored ammunition");
        results.Add("PASS CharacterState vitals serialize correctly without replacing local skills or ammunition");
    }

    private static void CheckCharacterStateIdentity(List<string> results)
    {
        using (var fixture = new Fixture())
        {
            var first = fixture.Player.GetCharacterState(1);
            var second = fixture.Player.GetCharacterState(2);
            var states = (CharacterState[])typeof(PlayerController)
                .GetField("_characterStates", Private).GetValue(fixture.Player);
            Check(ReferenceEquals(first, states[0]) && ReferenceEquals(second, states[1]),
                "character lookups return the authoritative state objects");
            fixture.Health.SetHealth(45f, 100f);
            Check(Near(first.currentHealth, 45f), "active Health writes into the existing CharacterState");
            second.currentHealth = 37f;
            fixture.Health.TakeDamage(new DamageInfo(100f));
            CompleteDeath(fixture.Player);
            Check(ReferenceEquals(first, fixture.Player.GetCharacterState(1))
                && ReferenceEquals(second, fixture.Player.GetCharacterState(2))
                && Near(first.currentHealth, 0f) && Near(second.currentHealth, 37f)
                && Near(fixture.Health.CurrentHealth, 37f),
                "death replacement uses the same stored states without creating another health store");
            var uiRoot = new GameObject("State binding validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var manager = uiRoot.AddComponent<CharacterStateManager>();
                manager.BindCharacterStates(states);
                Check(ReferenceEquals(manager.GetState(1), first) && ReferenceEquals(manager.GetState(2), second),
                    "the local state manager shares the player's existing CharacterState objects");
                int hungerEvents = 0, thirstEvents = 0;
                manager.OnCharacterHungerChanged += (id, value, max) => { Check(id == 2, "hunger gauge uses active character"); hungerEvents++; };
                manager.OnCharacterThirstChanged += (id, value, max) => { Check(id == 2, "thirst gauge uses active character"); thirstEvents++; };
                manager.RefreshCharacterStates(2);
                Check(hungerEvents == 1 && thirstEvents == 1, "reserve stats do not overwrite active survival gauges");
            }
            finally { Object.DestroyImmediate(uiRoot); }
        }
        results.Add("PASS health changes and replacement retain the authoritative CharacterState objects");
    }

    private static void CheckDeathPending(Fixture fixture, int slot)
        => Check(fixture.Player.ActiveSlotIndex == slot && Near(fixture.Health.CurrentHealth, 0f)
            && fixture.Health.IsDead && fixture.Player.IsDeathPresented && fixture.Player.IsInputLocked
            && fixture.Player.IsWaitingForDeathAnimation,
            "HP zero retains the dying character and locks input until death playback completes");

    private static void CompleteDeath(PlayerController player)
        => typeof(PlayerController).GetMethod("UpdateDeathAnimation", Private).Invoke(player, null);

    private static bool RequestServerSlot(PlayerController player, int slot)
        => (bool)typeof(PlayerController).GetMethod("TryChangeCharacterSlot", Private)
            .Invoke(player, new object[] { slot });

    private static void AdvanceSurvival(PlayerController player, float seconds)
        => typeof(PlayerController).GetMethod("UpdateCharacterStates", Private)
            .Invoke(player, new object[] { seconds });

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Party regression failed: " + message);
    }

    private static void Set(object target, string field, object value)
        => target.GetType().GetField(field, Private).SetValue(target, value);

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject Root;
        public readonly Health Health;
        public readonly PlayerController Player;
        public int DeathEvents { get; private set; }

        public Fixture(PlayerController.CharacterDeckData? deck = null)
        {
            Root = new GameObject("Party validation (temporary)") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var body = Root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                Health = Root.AddComponent<Health>();
                Player = Root.AddComponent<PlayerController>();
                // Edit Mode does not run normal network spawn or guarantee Awake/OnEnable.
                Set(Player, "_health", Health);
                Set(Player, "_rb", body);
                SetServer(Health);
                SetServer(Player);
                var healthHandler = (Action<float, float>)Delegate.CreateDelegate(typeof(Action<float, float>), Player,
                    typeof(PlayerController).GetMethod("HandleCharacterHealthChanged", Private));
                Health.OnHealthChanged -= healthHandler;
                Health.OnHealthChanged += healthHandler;
                // Health's spawn method only binds its HP notifications; PlayerController's
                // spawn method additionally loads scene/UI objects and is deliberately skipped.
                Health.OnNetworkSpawn();
                var activeSlot = (NetworkVariable<int>)
                    typeof(PlayerController).GetField("_activeSlotIndex", Private).GetValue(Player);
                activeSlot.OnValueChanged += (NetworkVariable<int>.OnValueChangedDelegate)
                    Delegate.CreateDelegate(typeof(NetworkVariable<int>.OnValueChangedDelegate),
                        Player, typeof(PlayerController).GetMethod("OnActiveSlotChanged", Private));
                if (deck.HasValue) Player.ServerInitializeDeck(deck.Value);
                typeof(PlayerController).GetMethod("InitializeCharacterStates", Private)
                    .Invoke(Player, new object[] { Player.GetLocalDeck() });

                var deathHandler = (Action<DamageInfo>)Delegate.CreateDelegate(typeof(Action<DamageInfo>), Player,
                    typeof(PlayerController).GetMethod("HandleDeath", Private));
                Health.OnDeath -= deathHandler;
                Health.OnDeath += deathHandler;
                Health.OnDeath += _ => DeathEvents++;
            }
            catch
            {
                Object.DestroyImmediate(Root);
                throw;
            }
        }

        private static void SetServer(NetworkBehaviour behaviour)
            => typeof(NetworkBehaviour).GetProperty("IsServer").GetSetMethod(true)
                .Invoke(behaviour, new object[] { true });

        public void SetHealth(int charId, float hp, float maxHp = 100f)
        {
            if (Player.GetLocalDeck().GetCharId(Player.ActiveSlotIndex) == charId)
            {
                Health.SetHealth(hp, maxHp);
                return;
            }
            var state = Player.GetCharacterState(charId);
            Check(state != null, "test setup requires a character in the party");
            state.currentHealth = hp;
            state.maxHealth = maxHp;
        }

        public void Dispose()
        {
            if (Root != null) Object.DestroyImmediate(Root);
        }
    }
}
