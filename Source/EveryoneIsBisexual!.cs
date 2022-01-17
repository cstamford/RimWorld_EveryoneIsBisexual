using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace EveryoneIsBisexual
{
    [StaticConstructorOnStartup]
    public static class Main
    {
        static Main()
        {
            Harmony harmony = new Harmony("everyone.is.bisexual");
            harmony.PatchAll();
        }
    }

    // This patch redirects the foreach loop to our function which filters each item based on settings.
    [HarmonyDebug]
    [HarmonyPatch(typeof(PawnGenerator), "GenerateTraits")]
    public static class PawnGenerator__GenerateTraits
    {
        private static MethodInfo _method_allow_gay = AccessTools.PropertyGetter(typeof(PawnGenerationRequest), "AllowGay");
        private static MethodInfo _method_add_bisexual_trait = AccessTools.Method(typeof(PawnGenerator__GenerateTraits), "AddBisexualTrait");

        private static bool AddBisexualTrait(bool allow_gay, Pawn pawn)
        {
            if (allow_gay)
            {
                pawn.story.traits.GainTrait(new Trait(TraitDefOf.Bisexual, PawnGenerator.RandomTraitDegree(TraitDefOf.Bisexual), false));
            }

            return allow_gay;
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> il = instructions.ToList();

            for (int i = 0; i < il.Count; ++i)
            {
                // Find this:
                //
                //     if (request.AllowGay && (LovePartnerRelationUtility.HasAnyLovePartnerOfTheSameGender(pawn) || LovePartnerRelationUtility.HasAnyExLovePartnerOfTheSameGender(pawn)))
                //     {
                //         Trait trait = new Trait(TraitDefOf.Gay, PawnGenerator.RandomTraitDegree(TraitDefOf.Gay), false);
                //         pawn.story.traits.GainTrait(trait);
                //     }
                //
                if (il[i].Calls(_method_allow_gay))
                {
                    il.InsertRange(++i, new CodeInstruction[]
                    {
                        // Call our function with two args - the gayness boolean + the pawn from original function.
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call, _method_add_bisexual_trait),

                        // We're going to add this return value to the number of traits so it occupies its own "slot".
                        // This way, we never lose a trait for bisexuality.
                        new CodeInstruction(OpCodes.Ldloc_1),
                        new CodeInstruction(OpCodes.Add),
                        new CodeInstruction(OpCodes.Stloc_1),

                        // Finally, we push false onto the stack - ensuring that the original block of code for gayness never executes.
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                    });

                    return il.AsEnumerable();
                }
            }

            throw new Exception("Could not locate the correct instruction to patch - a mod incompatibility or game update broke it.");
        }
    }
}