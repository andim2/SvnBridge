namespace SvnBridge.SourceControl
{
    public enum Recursion
    {
        None,
        OneLevel,
        Full
        // SVNBRIDGE_WARNING_REF_RECURSION: specifying Recursion.Full is *dangerous*.
        // Despite some docs saying that recursion type (None, OneLevel, Full) applies to folders only,
        // e.g. in the case of QueryHistory() of a rather precise *fully qualified* request
        // on a "$/REPO/resource.h" *file-type* itemspec,
        // it would erroneously return *all* files called "resource.h" anywhere within this repo!
        // (at least on TFS2008)
        // I'm currently not yet sure how to deal with this thoroughly "special"
        // (for lack of more damning words) API behaviour
        // when transgressing towards the SVN side of things.
        // Possibly the best way is to apply (sufficiently universally) a post-processing filter as needed
        // (filter all items which don't have a beginning part which *exactly* matches the itemspec path
        // requested), and possibly even offer only the filtered (i.e., corrected) result
        // as official class interface API.
    }
}
