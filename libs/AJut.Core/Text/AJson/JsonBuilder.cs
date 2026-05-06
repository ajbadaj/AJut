namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// V2 builder for constructing json programmatically (no parsing involved). Walks an
    /// in-memory tree of builder nodes and emits a JsonValue tree at <see cref="Finalize"/>
    /// time. No text coherence machinery - serialization happens via <see cref="JsonWriter"/>
    /// when something asks for ToString.
    /// </summary>
    public sealed class JsonBuilder
    {
        private string m_value;

        // ===============================[ Construction ]===========================
        internal JsonBuilder (JsonBuilderSettings settings)
        {
            this.Key = String.Empty;
            this.BuilderSettings = settings ?? new JsonBuilderSettings();
            this.Children = new List<JsonBuilder>();
            this.ArrayIndex = -1;
        }

        internal JsonBuilder (JsonBuilder parent) : this(parent.BuilderSettings)
        {
            this.Parent = parent;
        }

        /// <summary>
        /// Root value builder constructor - converts the provided instance into a builder tree.
        /// </summary>
        internal JsonBuilder (JsonBuilderSettings settings, object value) : this(settings)
        {
            this.IsValue = true;
            JsonHelper.FillOutJsonBuilderForObject(value, this);
        }

        // ===============================[ Properties ]===========================
        public JsonBuilderSettings BuilderSettings { get; set; }

        public JsonBuilder Parent { get; private set; }

        public List<JsonBuilder> Children { get; private set; }

        public int ArrayIndex { get; private set; }

        /// <summary>
        /// Set if this builder represents a document key/value pair entry.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Set if this builder represents a document key/value pair entry - the value side of the pair.
        /// </summary>
        public JsonBuilder DocumentKVPValue { get; internal set; }

        public bool IsUnset => !this.IsDocument && !this.IsArray && !this.IsValue;
        public bool IsDocument { get; private set; }
        public bool IsArray { get; private set; }
        public bool IsValue { get; private set; }

        public bool IsDocumentKVP => this.Key != String.Empty;
        public bool IsArrayItem => this.ArrayIndex != -1;

        public string Value
        {
            get => m_value;
            set
            {
                if (m_value != value)
                {
                    m_value = value;
                    this.IsValue = true;
                }
            }
        }

        public bool IsValueUsualQuoteTarget { get; set; }

        // ===============================[ Public Interface Methods ]===========================
        public JsonBuilder StartProperty (string propertyName)
        {
            if (this.IsUnset)
            {
                this.IsDocument = true;
            }

            if (this.IsDocument)
            {
                JsonBuilder child = new JsonBuilder(this);
                child.Key = propertyName;
                this.Children.Add(child);
                return child;
            }

            throw new InvalidOperationException("StartProperty can only be called on a document builder");
        }

        public JsonBuilder StartDocument ()
        {
            if (this.IsArray)
            {
                JsonBuilder arrayItem = new JsonBuilder(this);
                arrayItem.ArrayIndex = this.Children.Count;
                arrayItem.IsDocument = true;
                this.Children.Add(arrayItem);
                return arrayItem;
            }
            if (this.IsDocument)
            {
                throw new InvalidOperationException("Tried to start a document inside a document, did you mean to use AddProperty?");
            }
            if (this.IsValue)
            {
                throw new InvalidOperationException("Tried to start a document inside a value");
            }

            if (this.IsDocumentKVP)
            {
                this.DocumentKVPValue = new JsonBuilder(this);
                this.DocumentKVPValue.IsDocument = true;
                return this.DocumentKVPValue;
            }

            this.IsDocument = true;
            return this;
        }

        public JsonBuilder StartArray ()
        {
            if (this.IsArray)
            {
                JsonBuilder arrayItem = new JsonBuilder(this);
                arrayItem.ArrayIndex = this.Children.Count;
                arrayItem.IsArray = true;
                this.Children.Add(arrayItem);
                return arrayItem;
            }
            if (this.IsDocument)
            {
                throw new InvalidOperationException("Tried to start an array inside a document, did you mean to use AddProperty?");
            }
            if (this.IsValue)
            {
                throw new InvalidOperationException("Tried to start an array inside a value");
            }

            if (this.IsDocumentKVP)
            {
                this.DocumentKVPValue = new JsonBuilder(this);
                this.DocumentKVPValue.IsArray = true;
                return this.DocumentKVPValue;
            }

            this.IsArray = true;
            return this;
        }

        public JsonBuilder AddProperty (string propertyName, object propertyValue, bool isUsuallyQuoted = true)
        {
            if (this.IsDocument)
            {
                if (propertyValue != null)
                {
                    JsonBuilder child = new JsonBuilder(this);
                    child.Key = propertyName;
                    child.DocumentKVPValue = new JsonBuilder(child);
                    child.DocumentKVPValue.IsValueUsualQuoteTarget = isUsuallyQuoted;
                    child.DocumentKVPValue.IsValue = true;
                    JsonHelper.FillOutJsonBuilderForObject(propertyValue, child.DocumentKVPValue);

                    this.Children.Add(child);
                }
                return this;
            }

            throw new InvalidOperationException("AddProperty can only be called on documents");
        }

        public JsonBuilder AddArrayItem (object arrayValue)
        {
            if (this.IsArray)
            {
                JsonBuilder child = new JsonBuilder(this);
                child.IsValue = true;
                JsonHelper.FillOutJsonBuilderForObject(arrayValue, child);

                this.Children.Add(child);
                return this;
            }

            throw new InvalidOperationException("AddArrayItem can only be called on an array");
        }

        public JsonBuilder GetParent ()
        {
            JsonBuilder target = this.Parent;
            if (target == null)
            {
                return null;
            }
            if (target.IsDocumentKVP)
            {
                target = target.Parent;
            }
            return target;
        }

        public JsonBuilder End () => this.GetParent() ?? this;

        public JsonBuilder FindRoot ()
        {
            JsonBuilder target = this.GetParent() ?? this;
            while (true)
            {
                JsonBuilder next = target.GetParent();
                if (next == null)
                {
                    break;
                }
                target = next;
            }
            return target;
        }

        public Json Finalize ()
        {
            JsonBuilder root = this.FindRoot();
            JsonValue data = root.BuildJsonValue();

            Json output = new Json();
            output.Data = data;
            return output;
        }

        // ===============================[ Helper Methods ]===========================
        internal JsonValue BuildJsonValue ()
        {
            if (this.IsDocument)
            {
                JsonDocument doc = new JsonDocument(this.Children.Count);
                foreach (JsonBuilder child in this.Children)
                {
                    if (child.DocumentKVPValue == null)
                    {
                        continue;
                    }

                    JsonValue childValue = child.DocumentKVPValue.BuildJsonValue();
                    doc.Add(child.Key, childValue);
                }
                return doc;
            }

            if (this.IsArray)
            {
                JsonArray arr = new JsonArray(this.Children.Count);
                foreach (JsonBuilder child in this.Children)
                {
                    JsonValue childValue = child.BuildJsonValue();
                    arr.Add(childValue);
                }
                return arr;
            }

            if (this.IsValue)
            {
                return new JsonValue(this.Value, isQuoted: this.IsValueUsualQuoteTarget);
            }

            // Empty / unset builder - represent as an empty value rather than throwing.
            return new JsonValue(String.Empty, isQuoted: false);
        }
    }
}
