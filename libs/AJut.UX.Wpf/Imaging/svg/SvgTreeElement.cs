namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Media;
    using AJut.Storage;
    using AJut.Tree;

    /// <summary>
    /// A tree element loaded inside of an <see cref="SvgSource"/>
    /// </summary>
    public class SvgTreeElement : ObservableTreeNode<SvgTreeElement>
    {
        private Lazy<Transform> m_finalTransformStorage;
        static SvgTreeElement ()
        {
            TreeTraversal<SvgTreeElement>.SetupDefaults(i => i.Children, i => i.Parent);
        }

        public SvgTreeElement (string id, Transform localTransform)
        {
            this.Id = id;
            this.LocalTransform = localTransform;
            this.InvalidateFinalTransform();
        }

        public SvgTreeElement (string id, Geometry geom, Brush fillBrush, double strokeWidth, Brush strokeBrush, double opacity = 1.0)
        {
            this.Id = id;
            this.Data = geom;
            this.FillBrush = fillBrush;
            this.StrokeWidth = strokeWidth;
            this.StrokeBrush = strokeBrush;
            this.Opacity = opacity;
            this.InvalidateFinalTransform();
        }

        public SvgTreeElement Duplicate ()
        {
            var duplicate = new SvgTreeElement(m_id, this.Data, m_fillBrush, m_strokeWidth, m_strokeBrush);
            foreach (var child in this.Children)
            {
                duplicate.AddChild(child.Duplicate());
            }

            return duplicate;
        }

        /// <summary>
        /// The element's id - this is supposed to be unique, so take care modifying this
        /// </summary>
        private string m_id = Guid.NewGuid().ToString();
        public string Id
        {
            get => m_id;
            set => this.SetAndRaiseIfChanged(ref m_id, value);
        }

        /// <summary>
        /// The element's opacity
        /// </summary>
        private double m_opacity = 1.0;
        public double Opacity
        {
            get => m_opacity;
            set => this.SetAndRaiseIfChanged(ref m_opacity, value);
        }

        /// <summary>
        /// The local transform applied to this element in relation to it's parent
        /// </summary>
        private Transform m_localTransform;
        public Transform LocalTransform
        {
            get => m_localTransform;
            set => this.SetAndRaiseIfChanged(ref m_localTransform, value);
        }

        /// <summary>
        /// The final transform applied after collapsing all parent transforms
        /// </summary>
        public Transform FinalTransform => m_finalTransformStorage.Value;

        /// <summary>
        /// The geometry of this element (if it's a path element)
        /// </summary>
        public Geometry Data { get; }

        /// <summary>
        /// The fill brush used when drawing this element
        /// </summary>
        private Brush m_fillBrush;
        public Brush FillBrush
        {
            get => m_fillBrush;
            set => this.SetAndRaiseIfChanged(ref m_fillBrush, value);
        }

        /// <summary>
        /// The resulting drawn width used when stroking this
        /// </summary>
        private double m_strokeWidth;
        public double StrokeWidth
        {
            get => m_strokeWidth;
            set => this.SetAndRaiseIfChanged(ref m_strokeWidth, value);
        }
        
        /// <summary>
        /// The brush used to draw the line for this node
        /// </summary>
        private Brush m_strokeBrush;
        public Brush StrokeBrush
        {
            get => m_strokeBrush;
            set => this.SetAndRaiseIfChanged(ref m_strokeBrush, value);
        }

        private void InvalidateFinalTransform ()
        {
            m_finalTransformStorage = new Lazy<Transform>(_CalculateCurrentTransform);
            this.RaisePropertyChanged(nameof(FinalTransform));

            Transform _CalculateCurrentTransform ()
            {
                Stack<Transform> transformStack = new Stack<Transform>();
                SvgTreeElement node = this;
                while (node != null)
                {
                    if (node.LocalTransform != null)
                    {
                        transformStack.Push(node.LocalTransform);
                    }
                    node = node.Parent;
                }

                TransformGroup final = new TransformGroup();
                final.Children.Add(Transform.Identity);
                while (transformStack.Count > 0)
                {
                    final.Children.Add(transformStack.Pop());
                }

                return final;
            }
        }
    }
}
