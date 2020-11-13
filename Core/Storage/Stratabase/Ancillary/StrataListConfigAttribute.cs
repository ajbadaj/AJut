namespace AJut.Storage
{
    using System;
    
    public enum eStrataListConfig
    {
        GenerateInsertOverrides,
        GenerateIndexedSubProperties,
        StoreListDirectly,
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
        public bool BuildReferenceList { get; }
        public static StrataListConfigAttribute Default { get; set; } = new StrataListConfigAttribute();
    }
}
