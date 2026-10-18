namespace ScrollToArms
{
    /// <summary>
    /// The picker's rules, with no game in them: what the wheel stops on, what holds a pick back,
    /// who the wheel belongs to and when a pick goes into the hand. The picker reads the facts from
    /// the game and asks here; see <see cref="Picker"/>.
    /// </summary>
    internal static class PickRules
    {
        /// <summary>
        /// A slot the cursor stops on: something held in the hands that can be equipped now.
        /// Food, armour and materials are passed over, and so is a broken item, which the game
        /// would refuse. A tool that opens build mode is passed over when SkipBuildTools is on, and
        /// so is anything on the SkipItems list.
        /// </summary>
        public static bool IsStop(bool equipable, ItemDrop.ItemData.ItemType type, bool buildTool, bool skipBuildTools, bool listed, bool wears, float durability)
        {
            if (!equipable) return false;

            switch (type)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Tool:
                    break;
                default:
                    return false;
            }

            if (skipBuildTools && buildTool) return false;
            if (listed) return false;

            return !(wears && durability <= 0f);
        }

        /// <summary>
        /// Why the game would refuse or lose a pick right now, or null. Attacking, dodging and
        /// swimming refuse any equip. Running clears the equip queue every frame
        /// (Player.CheckRun), so an item with an equip time cannot arrive while running, while one
        /// without still swaps straight away, and so does the stowed item, which comes back
        /// through the Hide key's own path.
        /// </summary>
        public static string Busy(bool attacking, bool dodging, bool swimming, bool onGround, bool equipTime, bool running, bool stowed)
        {
            if (attacking) return "attacking";
            if (dodging) return "dodging";
            if (swimming && !onGround) return "swimming";
            if (equipTime && running && !stowed) return "running";
            return null;
        }

        /// <summary>
        /// Whether the wheel belongs to the hotbar, once the hotbar can be used at all. In build
        /// mode the plain wheel rotates the piece, whatever PlainScroll says, so the hotbar is
        /// always the modifier's there, and only once the rotation's wheel read is redirected.
        /// </summary>
        public static bool OwnsWheel(bool modifierHeld, bool placing, bool placementReady, PlainScroll plain)
        {
            if (placing) return modifierHeld && placementReady;

            return plain == PlainScroll.Zoom ? modifierHeld : !modifierHeld;
        }

        /// <summary>
        /// The commit that applies. Releasing the modifier can only end a pick that was made with it
        /// held, so when plain scroll drives the hotbar the pause is used instead.
        /// </summary>
        public static Commit EffectiveCommit(PlainScroll plain, Commit setting) =>
            plain == PlainScroll.Hotbar ? Commit.AfterPause : setting;
    }
}
