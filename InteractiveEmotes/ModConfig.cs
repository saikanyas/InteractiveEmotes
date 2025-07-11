namespace InteractiveEmotes
{
    /// <summary>Defines the user-configurable settings for the mod.</summary>
    public sealed class ModConfig
    {
        /// <summary>Gets or sets the maximum distance in tiles that a character can be to react to an emote.</summary>
        public int EventDistance { get; set; } = 3;
        /// <summary>Gets or sets the delay in milliseconds before a character responds.</summary>
        public int EmoteDelay { get; set; } = 700;
        /// <summary>Gets or sets a value indicating whether to play a sound effect when a character reacts.</summary>
        public bool PlayReplySound { get; set; } = true;

        /// <summary>Gets or sets the amount of friendship points gained when an NPC reacts positively.</summary>
        public int FriendshipGainAmount { get; set; } = 10;
        /// <summary>Gets or sets a value indicating whether to show a HUD message when friendship is gained.</summary>
        public bool ShowFriendshipGainMessage { get; set; } = true;

        /// <summary>Gets or sets a value indicating whether the emote combo system is enabled.</summary>
        public bool EmoteCombo { get; set; } = true;

        /// <summary>Gets or sets the combo counting mode. "PerCombo" uses the TriggerCount from JSON. "Fixed" uses the GlobalComboTarget.</summary>
        public string ComboCountMode { get; set; } = "PerCombo";

        /// <summary>Gets or sets the number of emotes required for a combo when using "Fixed" mode, or as a fallback for "PerCombo" mode.</summary>
        public int GlobalComboTarget { get; set; } = 3;

        /// <summary>Gets or sets the time in milliseconds before a combo sequence resets.</summary>
        public int ComboTimeout { get; set; } = 2100;

        /// <summary>Gets or sets a value indicating whether 'Weather' conditions in the rules are checked.</summary>
        public bool EnableWeatherConditions { get; set; } = true;
        /// <summary>Gets or sets a value indicating whether 'Season' conditions in the rules are checked.</summary>
        public bool EnableSeasonConditions { get; set; } = true;
        /// <summary>Gets or sets a value indicating whether friendship-related conditions in the rules are checked.</summary>
        public bool EnableFriendshipConditions { get; set; } = true;

        /// <summary>Gets the event distance converted to pixels for internal calculations.</summary>
        [Newtonsoft.Json.JsonIgnore]
        public int EventDistanceInPixels => EventDistance * 64;
    }

    /// <summary>The API interface for Generic Mod Config Menu, used for registration.</summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(StardewModdingAPI.IManifest mod, System.Action reset, System.Action save, bool titleScreenOnly = false);
        void AddSectionTitle(StardewModdingAPI.IManifest mod, System.Func<string> text, System.Func<string>? tooltip = null);
        void AddBoolOption(StardewModdingAPI.IManifest mod, System.Func<bool> getValue, System.Action<bool> setValue, System.Func<string> name, System.Func<string>? tooltip = null, string? fieldId = null);
        void AddNumberOption(StardewModdingAPI.IManifest mod, System.Func<int> getValue, System.Action<int> setValue, System.Func<string> name, System.Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, System.Func<int, string>? formatValue = null, string? fieldId = null);
        void AddTextOption(StardewModdingAPI.IManifest mod, System.Func<string> getValue, System.Action<string> setValue, System.Func<string> name, System.Func<string>? tooltip = null, string[]? allowedValues = null, System.Func<string, string>? formatAllowedValue = null, string? fieldId = null);
    }
}