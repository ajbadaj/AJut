namespace AJut.UX
{
    using System;

    /// <summary>
    /// Which name seeds the crypto obfuscation defaults during application setup
    /// </summary>
    public enum eCryptoSeedSource
    {
        /// <summary>
        /// Seed from the shared project name when there is one, otherwise the project name - so the seed follows the
        /// storage root, and projects sharing a root can read each other's obfuscated data. This is what WinUI3 has
        /// always done, and it is the recommended setting.
        /// </summary>
        SharedProjectNameFirst,

        /// <summary>
        /// Seed from the project name alone, even when a shared project name is set. This is what the older WPF setup
        /// overload has always done - pick it to keep reading obfuscated data a WPF project has already written.
        /// </summary>
        ProjectNameOnly,
    }

    /// <summary>
    /// Everything application setup needs, shared by the WPF and WinUI3 surfaces so a project targeting both writes one
    /// config instead of two parameter lists that have quietly drifted apart from each other.
    /// </summary>
    public class ApplicationSetupConfig
    {
        // ===========[ Setup/Construction/Teardown ]===================================

        public ApplicationSetupConfig (string projectName)
        {
            this.ProjectName = projectName;
        }

        // ===========[ Properties ]===================================

        /// <summary>
        /// The name of the project, used for context in logging and potentially elsewhere
        /// </summary>
        public string ProjectName { get; }

        /// <summary>
        /// A shared project name so two or more projects can share a root location (ie CoolProj is the shared project
        /// name, but individually the projects are: CoolProjClient, CoolProjServer)
        /// </summary>
        public string? SharedProjectName { get; init; }

        /// <summary>
        /// Should logging be setup, will default to a project name specific appdata location
        /// </summary>
        public bool SetupLogging { get; init; } = true;

        /// <summary>
        /// The max age (in days) to keep logs - setup will auto purge logs older than this. Pass in -1 to skip log
        /// purging (not recommended).
        /// </summary>
        public int AgeMaxInDaysToKeepLogs { get; init; } = 10;

        /// <summary>
        /// Something to handle unhandled exceptions, or null to leave the application's exception hooks alone
        /// </summary>
        public ApplicationExceptionProcessor? OnExceptionReceived { get; init; }

        /// <summary>
        /// What <see cref="Environment.SpecialFolder"/> to keep things like logs in, or null for the traditional default
        /// of whichever stack is running setup (roaming app data on WPF, local app data on WinUI3). Ignored entirely
        /// when a <see cref="StorageRootOverride"/> is set.
        /// </summary>
        public Environment.SpecialFolder? ApplicationStorageRoot { get; init; }

        /// <summary>
        /// The exact folder to use as the app data root. This wins over <see cref="ApplicationStorageRoot"/>, and unlike
        /// that property it is taken verbatim - no project name is appended to it. That is deliberate: the reason to set
        /// this is to land on a storage root somebody else already picked (say a packaged parent app whose child process
        /// you are), and appending a project name would put you next to that root instead of on it.
        /// </summary>
        public string? StorageRootOverride { get; init; }

        /// <summary>
        /// Which name seeds crypto obfuscation, or null to take the running stack's default. WinUI3 defaults to
        /// <see cref="eCryptoSeedSource.SharedProjectNameFirst"/>. WPF has no safe default when a
        /// <see cref="SharedProjectName"/> is set, because the two answers produce different keys and neither one can be
        /// picked for you without risking data somebody has already written - so WPF setup asks for it outright.
        /// <para>
        /// Neither value is a cage: the seeding happens during setup, so calling
        /// <see cref="AJut.Security.CryptoObfuscation.SeedDefaults"/> yourself afterwards replaces it with whatever you like.
        /// </para>
        /// </summary>
        public eCryptoSeedSource? CryptoSeedSource { get; init; }

        /// <summary>
        /// The project name that seeds the storage root: the shared project name when there is one, otherwise the
        /// project name.
        /// </summary>
        public string StorageRootProjectName => this.SharedProjectName ?? this.ProjectName;

        // ===========[ Public Interface Methods ]===================================

        /// <summary>
        /// The string to seed crypto obfuscation with, per <see cref="CryptoSeedSource"/>. An unset source resolves the
        /// same way <see cref="eCryptoSeedSource.SharedProjectNameFirst"/> does.
        /// </summary>
        public string DetermineCryptoSeed ()
        {
            return this.CryptoSeedSource == eCryptoSeedSource.ProjectNameOnly ? this.ProjectName : this.StorageRootProjectName;
        }
    }
}
