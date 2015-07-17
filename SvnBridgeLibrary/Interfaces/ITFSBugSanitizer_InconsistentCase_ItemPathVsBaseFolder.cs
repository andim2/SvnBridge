namespace SvnBridge.Interfaces
{
    using CodePlex.TfsLibrary.RepositoryWebSvc; // VersionSpec

    /// <summary>
    /// TFS (at least TFS2013, but quite likely also TFS2008 etc.)
    /// somehow manages to produce veritable, true cases of TFS SCM history
    /// indicating WRONGLY-cased, non-existent items (even within single commits!)
    /// as a sibling of the actually CORRECTLY-cased, non-referenced item,
    /// *within one commit*.
    /// IOW, we have glaring inconsistencies
    /// of within-commit filesystem item case sensitivity
    /// --> "database corruption"!!
    /// This case mismatch e.g. happened
    /// for items which are parent dirs
    /// of the item occurring in the commit.
    /// One could very strongly argue
    /// that such directly inconsistent commit data is TERRIBLY B0RKEN,
    /// irrespective of whether TFS does or does not have contracted
    /// the case insensitive disease in general.
    /// Fortunately, even for such broken cases,
    /// querying TFS for this parent dir item directly
    /// (and at the same revision as the sub item)
    /// seems to successfully deliver a now correctly-cased result item!
    ///
    /// This interface is thus expected to represent
    /// the strict minimum capability
    /// required to achieve corrected paths
    /// (an implementation of this interface
    /// will then probably do things such as
    /// multiple requests to TFS,
    /// to fully properly figure out the actually valid item path,
    /// in either cached or woefully slow uncached manner).
    ///
    /// One should try hard to fix all such pretty much worst (fatal) issues
    /// directly at the pretty much earliest (read: near the guilty party!)
    /// interface layer transfer opportunity.
    ///
    /// Note that I do not think that we need to offer
    /// a second method (item *ID* variant) here,
    /// since the only cases where we need to verify things
    /// are *after* having received reply data from TFS
    /// (requested via either item path *or* ID),
    /// where the questionable result data
    /// then is available in *path*-based form already.
    /// </summary>
    /// Details of filesystem item case sensitivity issue:
    /// When moving a file item from one directory to another,
    /// the item got recorded as having an INCORRECT / WRONG parent folder
    /// (e.g. "parentdir" rather than "PARENTDIR", whereas the repository contains a "PARENTDIR" *only*
    /// i.e. does **NOT** contain a "parentdir"!)
    /// Seems like the TFS database has some awful data redundancy
    /// (in addition to the parent folder which actually contains the sub items,
    /// sub items seem to get equipped with their FQPN,
    /// which will then end up wrong i.e. inconsistent in certain cases!!!).
    /// First Law of databases (right? :): data redundancy --> inconsistency.
    /// Since the file item will inherit a wrong FQPN
    /// *and will be listed with that wrong inexistent path in all subsequent changes*,
    /// *every single* item lookup is suspicious,
    /// thus there remains nothing to do for us
    /// other than painfully correcting every single item path in full.
    /// Unfortunately we could not figure out / recollect any more
    /// what the specific reason for this particular commit case sensitivity issue was
    /// (i.e., how it was produced/introduced
    /// and ended up getting committed with WRONG data by TFS).
    /// Side note: git-tfs is said to be doing case corrections
    /// in its GitRepository.ParseEntries method.
    public interface ITFSBugSanitizer_InconsistentCase_ItemPathVsBaseFolder
    {
        /// <summary>
        /// Results in an item path
        /// which has been verified to actually be properly correct -
        /// the result may thus have been corrected
        /// i.e. freed from certain TFS issues such as:
        /// - the TFS item invalid case sensitivity disease
        /// ,
        /// analyzed at exactly the item revision (version)
        /// which has been specified.
        /// Recommended sample invocation pattern:
        ///     string pathToBeChecked = dataItemToPotentiallyBeCorrected;
        ///     bool haveEncounteredAnyMismatch = MakeItemPathSanitized(
        ///         ref pathToBeChecked,
        ///         versionSpec,
        ///         itemType);
        ///     bool hadSanePath = !(haveEncounteredAnyMismatch);
        ///     if (!(hadSanePath))
        ///     {
        ///         dataItemToPotentiallyBeCorrected = pathToBeChecked;
        ///     }
        /// </summary>
        /// Performance-related implementation details:
        /// PLEASE NOTE that this API is as much of a *terrible* hotpath
        /// as it can ever get,
        /// since this correction handler
        /// may easily be executed for thousands of items, at all times!!
        /// (IOW avoid any bloat whatsoever in your implementation!)
        ///
        /// I decided to provide this as bool result / string reference
        /// (rather than returning a potentially modified string),
        /// since that way the "strings mismatching" decision
        /// can be carried out internally already
        /// (i.e., does not need to be done manually *each time*
        /// in user-side instruction cache layers) -
        /// and in fact the decision
        /// on whether values are to be considered mismatching or not
        /// *can* definitely be seen as internal authority knowledge of the implementation side
        /// (and of each [potentially differing] implementation variant also -
        /// otherwise one could have a central helper here
        /// which does the comparison centrally for all implementations).
        ///
        /// Performance note: I decided to alter interface
        /// to directly require a VersionSpec
        /// rather than an int-based version value, reasons:
        /// - otherwise every single MakeItemPathSanitized() request
        ///   would need to laboriously create a temporary VersionSpec internally,
        ///   rather than often being able to reuse
        ///   a globally available (per-whole-Changeset) version value
        ///   (while some users
        ///   do need to create a new VersionSpec value each time,
        ///   the overhead that we save for the users
        ///   which do not need to do so
        ///   is worth more...)
        /// - this bug is fully TFS-specific,
        ///   thus we are able to (and it is a good idea)
        ///   to be directly making use of
        ///   helper types specific to that toolkit
        /// <param name="pathToBeChecked">Full SCM path of item - potentially tweaked (sanitized)</param>
        /// <param name="versionSpec">Version to query items of</param>
        /// <param name="itemType">Type of item, at innermost (read: full) sub path location</param>
        /// <returns>bool which indicates whether the path string has been sanitized</returns>
        bool MakeItemPathSanitized(
            ref string pathToBeChecked,
            VersionSpec versionSpec,
            ItemType itemType);
    }
}
