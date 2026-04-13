using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.Blueprints.Root;
using Kingmaker.Localization;
using Kingmaker.Sound;
using Kingmaker.Visual.Sound;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace VoicePortsKingmaker;

public static class Main
{
    internal static Harmony HarmonyInstance;
    internal static UnityModManager.ModEntry.ModLogger Log;

    private static readonly VoiceDefinition[] Voices =
    {
        new VoiceDefinition("Amiri", "VoicePortAmiri", VoiceGender.Female, "442b16b1932849ba9cb7888bcf075974"),
        new VoiceDefinition("Cephal Lorentus", "VoicePortCephal", VoiceGender.Male, "7b6bdc7de2b24f6995f94329495e284a"),
        new VoiceDefinition("Ekundayo", "VoicePortEkundayo", VoiceGender.Male, "cfedf2af03ea4cda96f9519bd676327e"),
        new VoiceDefinition("Harrim", "VoicePortHarrim", VoiceGender.Male, "c79bd198565b458ea57ae6f46703735a"),
        new VoiceDefinition("Jaethal", "VoicePortJaethal", VoiceGender.Female, "d20a5fbeda4e423184474d3923e150aa"),
        new VoiceDefinition("Jubilost", "VoicePortJubilost", VoiceGender.Male, "ea1f5d8f67f84dfbaa65b3447becb83d"),
        new VoiceDefinition("Kalikke", "VoicePortKalikke", VoiceGender.Female, "146b75c0fc6d4eb9be0e698d982c41b5"),
        new VoiceDefinition("Kanerah", "VoicePortKanerah", VoiceGender.Female, "30ee64531d3a4be69a75015c9f7372a1"),
        new VoiceDefinition("Linzi", "VoicePortLinzi", VoiceGender.Female, "7f4f764aa3ed4e07aefa1c21dc94f320"),
        new VoiceDefinition("Maegar Varn", "VoicePortVarn", VoiceGender.Male, "00fe0dad37e54bf4be9d3d5a870eda58"),
        new VoiceDefinition("Nok-Nok", "VoicePortNokNok", VoiceGender.Male, "b9db4bf8ea2048dab5fef024c222487f"),
        new VoiceDefinition("Octavia", "VoicePortOctavia", VoiceGender.Female, "8bc4dd1a51354d84b84a385399c2a42b"),
        new VoiceDefinition("Regongar", "VoicePortRegongar", VoiceGender.Male, "f5cad41b0ef1424d9469f26cd2e74b17"),
        new VoiceDefinition("Tartuccio", "VoicePortTartuccio", VoiceGender.Male, "3a0c32401a8f42a6bfc9b35545f28841"),
        new VoiceDefinition("Tartuk", "VoicePortTartuk", VoiceGender.Male, "5a001f916a4d4eafb9d3e7a11dc9a400"),
        new VoiceDefinition("Tristian", "VoicePortTristian", VoiceGender.Male, "90c037fad7d94d398a62fd7f71b3ba7e"),
        new VoiceDefinition("Valerie", "VoicePortValerie", VoiceGender.Female, "253cb2887212476dad4949895fa3a4c9")
    };

    public static bool Load(UnityModManager.ModEntry modEntry)
    {
        Log = modEntry.Logger;
        modEntry.OnGUI = OnGUI;

        HarmonyInstance = new Harmony(modEntry.Info.Id);
        try
        {
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        }
        catch
        {
            HarmonyInstance.UnpatchAll(HarmonyInstance.Id);
            throw;
        }

        return true;
    }

    public static void OnGUI(UnityModManager.ModEntry modEntry)
    {
    }

    [HarmonyPatch]
    public static class Soundbanks
    {
        private static readonly Dictionary<string, VoiceDefinition> PreviewToVoice =
            Voices.ToDictionary(v => v.PreviewSound, v => v, StringComparer.OrdinalIgnoreCase);

        [HarmonyPatch(typeof(AkAudioService), nameof(AkAudioService.Initialize))]
        [HarmonyPostfix]
        public static void AddBankPaths()
        {
            var banksPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            AkSoundEngine.AddBasePath(banksPath);
        }

        [HarmonyPatch(typeof(UnitAsksComponent), nameof(UnitAsksComponent.PlayPreview))]
        [HarmonyPrefix]
        static bool LoadPreviewBank(UnitAsksComponent __instance)
        {
            if (__instance.PreviewSound is null)
                return true;

            if (!PreviewToVoice.TryGetValue(__instance.PreviewSound, out var voice))
                return true;

            if (!SoundBanksManager.s_Handles.Any(handle =>
                    string.Equals(handle.Key, voice.PreviewSound, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(handle.Key, voice.SoundBankName, StringComparison.OrdinalIgnoreCase)))
            {
                SoundBanksManager.LoadBankSync(voice.PreviewSound);
            }

            GameObject gameObject = Game.Instance.UI.Common.gameObject;
            uint previewEventId = AkSoundEngine.GetIDFromString(voice.PreviewSound);
            SoundEventsManager.PostEvent(previewEventId, gameObject);

            return false;
        }

        [HarmonyPatch(typeof(BlueprintsCache), nameof(BlueprintsCache.Init))]
        [HarmonyPostfix]
        static void AddAsksListBlueprints()
        {
            foreach (var voice in Voices)
            {
                AddVoiceBlueprint(voice);
            }
        }

        private static void AddVoiceBlueprint(VoiceDefinition voice)
        {
            LocalizationManager.CurrentPack.PutString(voice.LocalizationKey, voice.Name);

            var blueprint = new BlueprintUnitAsksList
            {
                AssetGuid = new(System.Guid.Parse(voice.GuidString)),
                name = $"{voice.InternalName}_Barks",
                DisplayName = new() { m_Key = voice.LocalizationKey }
            };

            blueprint.ComponentsArray =
            [
                new UnitAsksComponent()
                {
                    OwnerBlueprint = blueprint,
                    SoundBanks = [ voice.SoundBankName ],
                    PreviewSound = voice.PreviewSound,

                    Aggro = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CombatStart_01",
                                RandomWeight = 0.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CombatStart_02",
                                RandomWeight = 0.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CombatStart_03",
                                RandomWeight = 0.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = true,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    Pain = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Pain",
                                RandomWeight = 0.0f,
                                ExcludeTime = 0,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 2.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    Fatigue = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Fatigue",
                                RandomWeight = 0.0f,
                                ExcludeTime = 0,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 60.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    Death = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Death",
                                RandomWeight = 0.0f,
                                ExcludeTime = 0,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = true,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    Unconscious = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Unconscious",
                                RandomWeight = 0.0f,
                                ExcludeTime = 0,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = true,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    LowHealth = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_LowHealth_01",
                                RandomWeight = 0.0f,
                                ExcludeTime = 1,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_LowHealth_02",
                                RandomWeight = 0.0f,
                                ExcludeTime = 1,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 10.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    CriticalHit = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CharCrit_01",
                                RandomWeight = 0.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CharCrit_02",
                                RandomWeight = 0.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CharCrit_03",
                                RandomWeight = 0.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 0.7f,
                        ShowOnScreen = false
                    },

                    Order = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_AttackOrder_01",
                                RandomWeight = 0.0f,
                                ExcludeTime = 3,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_AttackOrder_02",
                                RandomWeight = 0.0f,
                                ExcludeTime = 3,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_AttackOrder_03",
                                RandomWeight = 0.0f,
                                ExcludeTime = 3,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_AttackOrder_04",
                                RandomWeight = 0.0f,
                                ExcludeTime = 3,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    OrderMove = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Move_01",
                                RandomWeight = 0.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Move_02",
                                RandomWeight = 0.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Move_03",
                                RandomWeight = 0.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Move_04",
                                RandomWeight = 0.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Move_05",
                                RandomWeight = 0.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Move_06",
                                RandomWeight = 0.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Move_07",
                                RandomWeight = 0.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 10.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 0.1f,
                        ShowOnScreen = false
                    },

                    Selected = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Select_01",
                                RandomWeight = 1.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Select_02",
                                RandomWeight = 1.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Select_03",
                                RandomWeight = 1.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Select_04",
                                RandomWeight = 1.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Select_05",
                                RandomWeight = 1.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Select_06",
                                RandomWeight = 1.0f,
                                ExcludeTime = 4,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_SelectJoke",
                                RandomWeight = 0.1f,
                                ExcludeTime = 30,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    RefuseEquip = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CantEquip_01",
                                RandomWeight = 1.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CantEquip_02",
                                RandomWeight = 1.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = true,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    RefuseCast = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CantCast",
                                RandomWeight = 0.0f,
                                ExcludeTime = 1,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = true,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    CheckSuccess = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CheckSuccess_01",
                                RandomWeight = 1.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CheckSuccess_02",
                                RandomWeight = 1.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    CheckFail = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CheckFail_01",
                                RandomWeight = 1.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_CheckFail_02",
                                RandomWeight = 1.0f,
                                ExcludeTime = 2,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    RefuseUnequip = new()
                    {
                        Entries = [],
                        Cooldown = 0.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    Discovery = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Discovery_01",
                                RandomWeight = 0.0f,
                                ExcludeTime = 1,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            },
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_Discovery_02",
                                RandomWeight = 0.0f,
                                ExcludeTime = 1,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    Stealth = new()
                    {
                        Entries =
                        [
                            new()
                            {
                                Text = null,
                                AkEvent = $"{voice.InternalName}_StealthMode",
                                RandomWeight = 1.0f,
                                ExcludeTime = 1,
                                m_RequiredFlags = [],
                                m_ExcludedFlags = [],
                                m_RequiredEtudes = null,
                                m_ExcludedEtudes = null
                            }
                        ],
                        Cooldown = 0.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    StormRain = new()
                    {
                        Entries = [],
                        Cooldown = 0.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    StormSnow = new()
                    {
                        Entries = [],
                        Cooldown = 0.0f,
                        InterruptOthers = false,
                        DelayMin = 0.0f,
                        DelayMax = 0.0f,
                        Chance = 1.0f,
                        ShowOnScreen = false
                    },

                    AnimationBarks =
                    [
                        new()
                        {
                            Entries =
                            [
                                new()
                                {
                                    Text = null,
                                    AkEvent = $"{voice.InternalName}_AttackShort",
                                    RandomWeight = 0.0f,
                                    ExcludeTime = 0,
                                    m_RequiredFlags = [],
                                    m_ExcludedFlags = [],
                                    m_RequiredEtudes = null,
                                    m_ExcludedEtudes = null
                                }
                            ],
                            Cooldown = 0.0f,
                            InterruptOthers = false,
                            DelayMin = 0.0f,
                            DelayMax = 0.0f,
                            Chance = 0.7f,
                            ShowOnScreen = false,
                            AnimationEvent = MappedAnimationEventType.AttackShort
                        },
                        new()
                        {
                            Entries =
                            [
                                new()
                                {
                                    Text = null,
                                    AkEvent = $"{voice.InternalName}_CoupDeGrace",
                                    RandomWeight = 0.0f,
                                    ExcludeTime = 0,
                                    m_RequiredFlags = [],
                                    m_ExcludedFlags = [],
                                    m_RequiredEtudes = null,
                                    m_ExcludedEtudes = null
                                }
                            ],
                            Cooldown = 0.0f,
                            InterruptOthers = true,
                            DelayMin = 0.0f,
                            DelayMax = 0.0f,
                            Chance = 1.0f,
                            ShowOnScreen = false,
                            AnimationEvent = MappedAnimationEventType.CoupDeGrace
                        },
                        new()
                        {
                            Entries = [],
                            Cooldown = 0.0f,
                            InterruptOthers = true,
                            DelayMin = 0.0f,
                            DelayMax = 0.0f,
                            Chance = 1.0f,
                            ShowOnScreen = false,
                            AnimationEvent = MappedAnimationEventType.Cast
                        },
                        new()
                        {
                            Entries = [],
                            Cooldown = 0.0f,
                            InterruptOthers = true,
                            DelayMin = 0.0f,
                            DelayMax = 0.0f,
                            Chance = 1.0f,
                            ShowOnScreen = false,
                            AnimationEvent = MappedAnimationEventType.CastDirect
                        },
                        new()
                        {
                            Entries = [],
                            Cooldown = 0.0f,
                            InterruptOthers = true,
                            DelayMin = 0.0f,
                            DelayMax = 0.0f,
                            Chance = 1.0f,
                            ShowOnScreen = false,
                            AnimationEvent = MappedAnimationEventType.CastLong
                        },
                        new()
                        {
                            Entries = [],
                            Cooldown = 0.0f,
                            InterruptOthers = true,
                            DelayMin = 0.0f,
                            DelayMax = 0.0f,
                            Chance = 1.0f,
                            ShowOnScreen = false,
                            AnimationEvent = MappedAnimationEventType.CastShort
                        },
                        new()
                        {
                            Entries = [],
                            Cooldown = 0.0f,
                            InterruptOthers = true,
                            DelayMin = 0.0f,
                            DelayMax = 0.0f,
                            Chance = 1.0f,
                            ShowOnScreen = false,
                            AnimationEvent = MappedAnimationEventType.CastTouch
                        },
                        new()
                        {
                            Entries = [],
                            Cooldown = 0.0f,
                            InterruptOthers = true,
                            DelayMin = 0.0f,
                            DelayMax = 0.0f,
                            Chance = 1.0f,
                            ShowOnScreen = false,
                            AnimationEvent = MappedAnimationEventType.CastYourself
                        },
                        new()
                        {
                            Entries = [],
                            Cooldown = 0.0f,
                            InterruptOthers = true,
                            DelayMin = 0.0f,
                            DelayMax = 0.0f,
                            Chance = 1.0f,
                            ShowOnScreen = false,
                            AnimationEvent = MappedAnimationEventType.Omnicast
                        },
                        new()
                        {
                            Entries = [],
                            Cooldown = 0.0f,
                            InterruptOthers = true,
                            DelayMin = 0.0f,
                            DelayMax = 0.0f,
                            Chance = 1.0f,
                            ShowOnScreen = false,
                            AnimationEvent = MappedAnimationEventType.Precast
                        },
                    ],
                },
            ];

            ResourcesLibrary.BlueprintsCache.AddCachedBlueprint(blueprint.AssetGuid, blueprint);

            var reference = blueprint.ToReference<BlueprintUnitAsksListReference>();

            if (voice.Gender == VoiceGender.Female)
                BlueprintRoot.Instance.CharGen.m_FemaleVoices =
                    BlueprintRoot.Instance.CharGen.m_FemaleVoices.Append(reference).ToArray();
            else
                BlueprintRoot.Instance.CharGen.m_MaleVoices =
                    BlueprintRoot.Instance.CharGen.m_MaleVoices.Append(reference).ToArray();
        }
    }

    public enum VoiceGender
    {
        Female,
        Male
    }

    public sealed class VoiceDefinition
    {
        public string Name { get; }
        public string InternalName { get; }
        public VoiceGender Gender { get; }
        public string GuidString { get; }

        public string SoundBankName => InternalName + "_GVR_ENG";
        public string PreviewSound => InternalName + "_Test";
        public string LocalizationKey => InternalName;

        public VoiceDefinition(string name, string internalName, VoiceGender gender, string guidString)
        {
            Name = name;
            InternalName = internalName;
            Gender = gender;
            GuidString = guidString;
        }
    }
}