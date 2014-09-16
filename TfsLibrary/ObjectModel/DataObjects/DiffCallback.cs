using System.Collections.Generic;

namespace CodePlex.TfsLibrary.ObjectModel
{
    public delegate void DiffCallback(string leftPathname,
                                      string leftVersion,
                                      string rightPathname,
                                      string rightVersion,
                                      List<DiffEngine.Chunk> diff,
                                      SourceItemResult result);
}