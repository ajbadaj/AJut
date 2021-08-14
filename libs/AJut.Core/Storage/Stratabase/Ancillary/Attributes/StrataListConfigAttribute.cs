namespace AJut.Storage
{
    using System;
    
    /// <summary>
    /// Indicates how to translate list properties to/from the stratabase
    /// </summary>
    public enum eStrataListConfig
    {
        /// <summary>
        /// The most complex list storage, this type of list is represented by <see cref="StrataPropertyListAccess{TElement}"/> - essentially list representation
        /// is a composite of inserts. Each layer stores inserts, and the access actually stores a cached current represetnation of the finally composited element storage
        /// </summary>
        GenerateInsertOverrides,

        /// <summary>
        /// Stores elements individually with name modification of index. Thus a list property Items were to be marked up this way, you would generate entries with property names like so: "Items[18]"
        /// </summary>
        GenerateIndexedSubProperties,

        /// <summary>
        /// Lists are stored directly in each layer. Thus if you had an array of ints marked up this way, you might make an access like so StrataPropertyAccess&lt;int[]&gt;
        /// </summary>
        StoreListDirectly,

        /// <summary>
        /// The default, <see cref="GenerateInsertOverrides"/>
        /// </summary>
        Default = GenerateInsertOverrides,
    }

    /// <summary>
    /// Makes it so you can generate list access instead of storing the whole list in each layer
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class StrataListConfigAttribute : Attribute
    {
        public StrataListConfigAttribute(eStrataListConfig config = eStrataListConfig.Default, Type elementType = null, bool buildReferenceList = false)
        {
            this.Config = config;
            this.ElementType = elementType;
            this.BuildReferenceList = buildReferenceList;
        }

        public eStrataListConfig Config { get; }

        public Type ElementType { get; }

        /// <summary>
        /// Indicates if the items in this list are things that should be stored in the stratabase as their own objects (must be legit objects with <see cref="StratabaseIdAttribute"/> set).
        /// This will make the stratabase representation be a list of reference access points (guids)
        /// </summary>
        public bool BuildReferenceList { get; }

        public static StrataListConfigAttribute Default { get; set; } = new StrataListConfigAttribute();
    }
}
