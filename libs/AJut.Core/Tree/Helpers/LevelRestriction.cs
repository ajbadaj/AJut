namespace AJut.Tree
{
    /// <summary>
    /// Helper with tracking user expectations of how far down the tree is allowed to be traversed
    /// </summary>
    public class LevelRestriction
    {
        private const int kAllowResultsAtAnyDepth = -1;

        /// <summary>
        /// The tree iteration depth that a node must happen on
        /// </summary>
        /// <example>
        /// ie only allow nodes to come from depth of 3 down from the root
        /// </example>
        public int RequiredLevel { get; set; } = kAllowResultsAtAnyDepth;

        /// <summary>
        /// The iteration depth MINIMUM that must be achieved before results can be considered
        /// </summary>
        public int LevelSearchMin { get; set; } = -1;

        /// <summary>
        /// The iteration depth MAXIMUM that will be considered before culling and or stopping tree search
        /// </summary>
        public int LevelSearchMax { get; set; } = -1;

        /// <summary>
        /// Set this if you would like to allow the algorithm to exit the starting depth. The main case for this would be if you
        /// started a depth-first search at an arbitrary point down the tree, ie 3 levels down, but you want the search to continue
        /// even when the end of the sub-tree is found (default = false)
        /// </summary>
        public bool AllowsExitingStartDepth { get; set; } = false;

        /// <summary>
        /// Construct a new restriction object
        /// </summary>
        public LevelRestriction () { }

        /// <summary>
        /// Construct a new restriction object
        /// </summary>
        public LevelRestriction (int requiredLevel)
        {
            this.RequiredLevel = requiredLevel;
            this.LevelSearchMin = requiredLevel;
            this.LevelSearchMax = requiredLevel;
        }

        /// <summary>
        /// Construct a new restriction object
        /// </summary>
        public LevelRestriction (int levelMin, int levelMax)
        {
            this.RequiredLevel = -1;
            this.LevelSearchMin = levelMin;
            this.LevelSearchMax = levelMax;
        }

        /// <summary>
        /// Evaluates if the given depth is valid given these <see cref="LevelRestriction"/>s
        /// </summary>
        /// <param name="testLevel">The tree depth currently being evaluated</param>
        /// <returns></returns>
        public bool Passes (int testLevel)
        {
            if (this.RequiredLevel == kAllowResultsAtAnyDepth)
            {
                // If we don't have a required depth, a minimum, or a maximum, than the answer is always true
                if (this.LevelSearchMin == kAllowResultsAtAnyDepth && this.LevelSearchMax == kAllowResultsAtAnyDepth)
                {
                    return true;
                }

                // Otherwise we don't have a required level, but we do have a min and/or max
                bool meetsMinimum = true;
                if (this.LevelSearchMin != kAllowResultsAtAnyDepth)
                {
                    meetsMinimum = testLevel >= this.LevelSearchMin;
                }

                bool meetsMaximum = true;
                if (this.LevelSearchMax != kAllowResultsAtAnyDepth)
                {
                    meetsMaximum = testLevel <= this.LevelSearchMax;
                }

                return meetsMinimum && meetsMaximum;
            }
            else
            {
                return this.RequiredLevel == testLevel;
            }
        }

        /// <summary>
        /// Indicates if the current <see cref="LevelRestriction"/> is currently enforcing depth limits given it's current state
        /// </summary>
        public bool EnforcesDepthLimits ()
        {
            return this.RequiredLevel != -1
                    && this.LevelSearchMin != -1
                    && this.LevelSearchMax != -1
                    && this.AllowsExitingStartDepth == false;
        }
    }

}
