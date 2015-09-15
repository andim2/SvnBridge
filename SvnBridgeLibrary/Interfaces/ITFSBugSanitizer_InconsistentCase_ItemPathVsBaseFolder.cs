namespace SvnBridge.Interfaces
{
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
        /// Returns an item path
        /// which has been verified to actually be properly correct -
        /// the returned result may thus have been corrected
        /// i.e. freed from certain TFS issues such as:
        /// - the TFS item invalid case sensitivity disease
        /// ,
        /// analyzed at exactly the item revision (version)
        /// which has been specified.
        /// </summary>
        /// Performance-related implementation details:
        /// PLEASE NOTE that this API is as much of a *terrible* hotpath
        /// as it can ever get,
        /// since this correction handler
        /// may easily be executed for thousands of items, at all times!!
        /// (IOW avoid any bloat whatsoever in your implementation!)
        ///
        /// <param name="pathToBeChecked">Full SCM path of item</param>
        /// <param name="revision">Revision to query items of</param>
        /// <returns>Potentially corrected item path</returns>
        string GetItemPathSanitized(
            string pathToBeChecked,
            int revision);
    }
}
