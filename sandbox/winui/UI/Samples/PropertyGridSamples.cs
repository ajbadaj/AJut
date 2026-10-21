namespace AJutShowRoomWinUI.UI.Samples
{
    using System;
    using System.Collections.Generic;
    using AJut;
    using AJut.TypeManagement;
    using AJut.UX;
    using AJut.UX.PropertyInteraction;
    using Windows.UI;

    // ===========[ ShowRoomTester - the complex property grid source ]=================
    // Exercises most of the property grid surface: groups, editors, elevation, conditional rows, coercion,
    // member ordering, buttons and the list variants. Two instances of it back the Complex A / Complex B
    // buttons so expansion memory across same-type sources can be checked.
    public class ShowRoomTester : NotifyPropertyChanged
    {

        private double m_value = 3.14;

        [PGEditor("Single")]
        [PGOverrideDefault(9001.0)]
        [PGGroup("Transform")]
        public double Value
        {
            get => m_value;
            set => this.SetAndRaiseIfChanged(ref m_value, value);
        }


        [PGEditor("ColorPick")]
        [PGGroup("Appearance")]
        public Color ColorValue { get; set; } = new Color { A = 255, B = 255 };
        [PGTypeAlias(typeof(ColorToStringConverter))]
        [PGGroup("Appearance")]
        public Color ColorAsString { get; set; } = new Color { A = 255, R = 255 };

        [MemberOrder(-1)] // force it to be first
        [PGLabel("Name", IconSource = "Assets/PenguinExample.png", IconMargin = 4)]
        [PGToolTip("The display name shown everywhere this object appears")]
        public string Name { get; set; } = "AJut Is Cool";
        public float? OptionalValue { get; set; }
        public ShowRoomSubObject SubObj { get; set; } = new ShowRoomSubObject();

        [PGElevateChildProperty(nameof(ShowRoomSubObject.SubObjValue))]
        [PGGroup("Transform")]
        public ShowRoomSubObject SubObjWithElevation { get; set; } = new ShowRoomSubObject();

        [PGEditor("SpecialString")]
        public TemplateSubType<string> SpecialStringEditor { get; set; } = new TemplateSubType<string>();

        public TemplateSubType<string> NormalStringEditor { get; set; } = new TemplateSubType<string>();

        // Exercises PGElevateAsParent + deferPGAttributesToParent + PGEditContextBuilder.
        // Before the fix, NumericEditor would show fallback Min/Max; after, it uses 0-100 / Step 0.5.
        [PGEditor("Single")]
        [PGEditContextBuilder("PG-Limits", "{ Min: 0.0, Max: 100.0, Step: 0.5 }")]
        [PGGroup("Transform")]
        public TemplateSubType<float> WrappedFloat { get; set; } = new TemplateSubType<float> { Value = 42.0f };

        // ------ ShowIf / HideIf demo ------
        // Change EditorMode to show/hide the matching editor row
        [PGGroup("Conditional")]
        public eEditorMode EditorMode { get; set; } = eEditorMode.Text;

        [PGShowIf(nameof(IsTextMode))]
        [PGGroup("Conditional")]
        public string TextModeValue { get; set; } = "Hello world";

        [PGShowIf(nameof(IsNumberMode))]
        [PGEditor("Single")]
        [PGGroup("Conditional")]
        public float NumberModeValue { get; set; } = 42.0f;

        [PGShowIf(nameof(IsColorMode))]
        [PGEditor("ColorPick")]
        [PGGroup("Conditional")]
        public Color ColorModeValue { get; set; } = new Color { A = 255, R = 128, G = 200, B = 255 };

        // Exercises PGShowIf + PGElevateAsParent(deferPGAttributesToParent: true)
        // with a method-based condition (matches the reported bug scenario).
        // When EditorMode is Number, this should appear alongside NumberModeValue.
        [PGShowIf(nameof(ShouldShowConditionalWrapped))]
        [PGEditor("Single")]
        [PGGroup("Conditional")]
        public TemplateSubType<float> ConditionalWrapped { get; set; } = new TemplateSubType<float> { Value = 77.0f };

        [PGHidden]
        public bool IsTextMode => this.EditorMode == eEditorMode.Text;
        [PGHidden]
        public bool IsNumberMode => this.EditorMode == eEditorMode.Number;
        [PGHidden]
        public bool IsColorMode => this.EditorMode == eEditorMode.Color;

        private bool ShouldShowConditionalWrapped () => this.EditorMode == eEditorMode.Number;

        // ------ PGCoerce demo ------
        // Value is clamped to 0-100 via custom coercion
        [PGCoerce(nameof(CoerceClampedValue))]
        [PGEditor("Single")]
        [PGGroup("Transform")]
        public float ClampedValue { get; set; } = 50.0f;

        private object CoerceClampedValue (object value)
        {
            if (value is double d)
            {
                return (float)Math.Clamp(d, 0.0, 100.0);
            }

            if (value is float f)
            {
                return Math.Clamp(f, 0.0f, 100.0f);
            }

            return value;
        }

        // ------ PGMemberOrder demo ------
        // Declared out of source order on purpose but tagged with [PGMemberOrder]. They render inside
        // the "PGMemberOrder Demo" group in order-value sequence (10, the button at 15, then 20),
        // proving a button interleaves between properties. The labels call out each expected slot. The
        // group's values are all positive, so it sorts after the unordered rows (the 0 baseline).
        [PGMemberOrder(20)]
        [PGGroup("PGMemberOrder Demo")]
        [PGLabel("Third (order=20)")]
        public string MemberOrderThird { get; set; } = "third";

        [PGMemberOrder(15)]
        [PGGroup("PGMemberOrder Demo")]
        [PGButton("Button (order=15)")]
        public void MemberOrderDemoButton ()
        {
            string temp = this.MemberOrderFirst;
            this.MemberOrderFirst = this.MemberOrderThird;
            this.MemberOrderThird = temp;
            this.RaisePropertyChanged(nameof(MemberOrderFirst));
            this.RaisePropertyChanged(nameof(MemberOrderThird));
        }

        [PGMemberOrder(10)]
        [PGGroup("PGMemberOrder Demo")]
        [PGLabel("First (order=10)")]
        public string MemberOrderFirst { get; set; } = "first";

        // ------ PGButton demo ------
        [PGButton("Reset Conditional Values")]
        [PGGroup("Conditional")]
        public void ResetConditionalValues ()
        {
            this.TextModeValue = "Hello world";
            this.NumberModeValue = 42.0f;
            this.ColorModeValue = new Color { A = 255, R = 128, G = 200, B = 255 };
        }

        // ------ PGList demos ------

        [PGList]
        [PGGroup("Lists")]
        public List<string> Tags { get; set; } = new List<string> { "alpha", "beta", "gamma" };

        [PGList]
        [PGGroup("Lists")]
        public float[] Weights { get; set; } = new float[] { 1.0f, 2.5f, 0.8f };

        [PGList(AddMethodName = nameof(AddCustomItem), RemoveMethodName = nameof(RemoveCustomItem))]
        [PGGroup("Lists")]
        public List<string> CustomListMethods { get; set; } = new List<string> { "custom-a", "custom-b" };

        private void AddCustomItem ()
        {
            this.CustomListMethods.Add($"custom-{this.CustomListMethods.Count}");
        }

        private void RemoveCustomItem (int index)
        {
            if (index >= 0 && index < this.CustomListMethods.Count)
            {
                this.CustomListMethods.RemoveAt(index);
            }
        }

        // ------ PGList with custom editor cascade demo ------

        // ------ External array replacement test ------
        // Models a real-life usage scenario where I noticed things broke.
        // Click "Swap List Externally" to replace the array.
        [PGList(AddMethodName = nameof(AddExternalItem))]
        [PGGroup("Lists")]
        public string[] ExternallyReplacedList
        {
            get => m_externalList;
            set
            {
                m_externalList = value;
                this.RaisePropertyChanged(nameof(ExternallyReplacedList));
            }
        }

        private string[] m_externalList = ["initial-a", "initial-b", "initial-c"];

        private void AddExternalItem ()
        {
            this.ExternallyReplacedList = [.. this.ExternallyReplacedList, $"added-{this.ExternallyReplacedList.Length}"];
        }

        [PGButton("Swap to 2 items")]
        [PGGroup("Lists")]
        public void SwapExternalListToTwo ()
        {
            this.ExternallyReplacedList = ["swapped-x", "swapped-y"];
        }

        [PGButton("Swap to empty")]
        [PGGroup("Lists")]
        public void SwapExternalListToEmpty ()
        {
            this.ExternallyReplacedList = [];
        }

        [PGButton("Swap to 5 items")]
        [PGGroup("Lists")]
        public void SwapExternalListToFive ()
        {
            this.ExternallyReplacedList = ["a", "b", "c", "d", "e"];
        }

        [PGList]
        [PGEditor("WidgetEditor")]
        [PGGroup("Lists")]
        public List<ShowRoomSubObject> ListWithEditor { get; set; } = new List<ShowRoomSubObject>
        {
            new ShowRoomSubObject { SubObjValue = 100 },
            new ShowRoomSubObject { SubObjValue = 200 },
        };

        // ------ PGList with elevation cascade demo ------

        [PGList]
        [PGElevateChildProperty(nameof(ShowRoomSubObject.SubObjValue))]
        [PGGroup("Lists")]
        public List<ShowRoomSubObject> ListWithElevation { get; set; } = new List<ShowRoomSubObject>
        {
            new ShowRoomSubObject { SubObjValue = 42 },
            new ShowRoomSubObject { SubObjValue = 99 },
        };

        // ------ PGList element header demo ------
        // Plain complex-element lists (no elevation, no custom editor), so each element row is expandable
        // with an otherwise-blank value column. ElementDisplayMemberName fills that column with the
        // element's Name. The first list shows the header in every state (Always); the second only while
        // the element row is collapsed (WhenCollapsed).
        [PGList(ElementDisplayMemberName = nameof(RosterEntry.Name))]
        [PGGroup("Lists")]
        public List<RosterEntry> Roster { get; set; } = new List<RosterEntry>
        {
            new RosterEntry { Name = "Tony", Age = 16 },
            new RosterEntry { Name = "Jill", Age = 24 },
            new RosterEntry { Name = "Jessup", Age = 31 },
        };

        [PGList(ElementDisplayMemberName = nameof(RosterEntry.Name), ElementDisplayMemberVisibility = eElementHeaderDisplay.WhenCollapsed)]
        [PGGroup("Lists")]
        public List<RosterEntry> RosterCollapsedHeaders { get; set; } = new List<RosterEntry>
        {
            new RosterEntry { Name = "Avery", Age = 19 },
            new RosterEntry { Name = "Blake", Age = 27 },
        };

        private class ColorToStringConverter : PropertyGridTypeAliasing<Color, string>
        {
            public override Type AliasType => typeof(string);

            public override Color ConvertFromAlias(string aliasValue)
            {
                if (CoerceUtils.TryGetColorFromString(aliasValue, out Color color))
                {
                    return color;
                }

                return default;
            }

            public override string ConvertToAlias(Color sourceValue)
            {
                return CoerceUtils.GetSmallestHexString(sourceValue);
            }
        }
    }

    public class ShowRoomSubObject : NotifyPropertyChanged
    {
        private int m_subObjValue = 9001;
        public int SubObjValue
        {
            get => m_subObjValue;
            set => this.SetAndRaiseIfChanged(ref m_subObjValue, value);
        }
    }

    // Element type for the PGList element-header demo - a plain complex object with a couple of
    // editable properties so the element rows expand and the value column is otherwise empty.
    public class RosterEntry : NotifyPropertyChanged
    {
        private string m_name;
        public string Name
        {
            get => m_name;
            set => this.SetAndRaiseIfChanged(ref m_name, value);
        }

        private int m_age;
        public int Age
        {
            get => m_age;
            set => this.SetAndRaiseIfChanged(ref m_age, value);
        }
    }

    public class TemplateSubType<T> : NotifyPropertyChanged
    {
        private T m_value;

        [PGElevateAsParent(deferPGAttributesToParent: true)]
        public T Value
        {
            get => m_value;
            set => this.SetAndRaiseIfChanged(ref m_value, value);
        }
    }


    // ===========[ ShowRoomAlpha - 5 properties ]=======
    // X=111, Y=222, Z=333 are very distinct from Beta so visual mismatch is immediately obvious.
    // Label (String) and IsActive (Bool) appear in Alpha but not Beta; if they persist after
    // switching to Beta the stale-display bug is confirmed.
    public class ShowRoomAlpha
    {
        public string Label { get; set; } = "Alpha Object";
        public float X { get; set; } = 111f;
        public float Y { get; set; } = 222f;

        [PGEditContextBuilder("PG-Limits", "{ Min: 0.0, Max: 180.0, Step: 1 }")]
        public float Z { get; set; } = 333f;

        [PGLabel("Active?", "Shows if it's active")]
        [PGToolTip("Toggle on or off", ShowName = false)]
        public bool IsActive { get; set; } = true;
    }

    [TypeId("PG-Limits")]
    public class Limits
    {
        public float Min { get; set; } = float.NegativeInfinity;
        public float Max { get; set; } = float.PositiveInfinity;
        public float Step { get; set; } = 1.0f;
        public float BigStep => this.Step * 5.0f;
        public float SmallStep => this.Step / 5.0f;

    }

    // ===========[ ShowRoomBeta - 2 properties ]=====
    // Switching Alpha -> Beta: 5 rows -> 2 rows, container count mismatch triggers WinUI3 recycling.
    // After switch the grid must show X=777, Y=888 - NOT Alpha's 111/222.
    public class ShowRoomBeta
    {
        public float X { get; set; } = 777f;
        public float Y { get; set; } = 888f;
    }

    // ===========[ MixedOrder - [MemberOrder] props + [PGMemberOrder] button ]=======
    // Reproduces the element-editing case: properties positioned with core [MemberOrder] across a
    // base/derived split, plus a button positioned with [PGMemberOrder]. The numbers read as one
    // sequence (1, 10, 11, 20, 21, 22) and the grid now sorts them on one axis, so the button slots
    // in after Height(21). Every row here is explicitly ordered, so there is no 0-baseline gap.
    public class MixedOrderBase : NotifyPropertyChanged
    {
        [PGEditor("Text")]
        [MemberOrder(1)]
        public string Name { get; set; } = "base name";
    }

    public class MixedOrderButtonSource : MixedOrderBase
    {
        [PGEditor("Text")]
        [MemberOrder(10)]
        public string SourcePath { get; set; } = "assets/example";

        [PGEditor("AutoEnum")]
        [MemberOrder(11)]
        public eEditorMode Mode { get; set; } = eEditorMode.Text;

        [PGEditor("Single")]
        [MemberOrder(20)]
        public float Width { get; set; } = 5f;

        [PGEditor("Single")]
        [MemberOrder(21)]
        public float Height { get; set; } = 5f;

        // Slots in after Height (21), since all rows share one ordering axis.
        [PGButton("Reset Size From Source")]
        [PGMemberOrder(22)]
        public void ResetSize ()
        {
            this.Width = 5f;
            this.Height = 5f;
        }
    }

    // Manual-PET equivalent of MixedOrderButtonSource: a hand-assembled edit manager. There are no
    // attributes to read here, so it positions every row - properties and the button - by setting
    // MemberSortOrder directly. CreateButton makes the button row; MemberSortOrder slots it after Height.
    public class ManualMixedOrderManager : IPropertyEditManager
    {
        private readonly MixedOrderButtonSource m_data = new MixedOrderButtonSource();

        public IEnumerable<PropertyEditTarget> GenerateEditTargets ()
        {
            yield return MakeRow("Name", "Text", () => m_data.Name, v => m_data.Name = v as string ?? "", 1);
            yield return MakeRow("SourcePath", "Text", () => m_data.SourcePath, v => m_data.SourcePath = v as string ?? "", 10);
            yield return MakeRow("Width", "Single", () => m_data.Width, v => m_data.Width = System.Convert.ToSingle(v), 20);
            yield return MakeRow("Height", "Single", () => m_data.Height, v => m_data.Height = System.Convert.ToSingle(v), 21);

            var button = PropertyEditTarget.CreateButton("Reset Size From Source", () => m_data.ResetSize());
            button.MemberSortOrder = 22;
            yield return button;
        }

        private static PropertyEditTarget MakeRow (string path, string editor, PropertyEditTarget.GetValue get, PropertyEditTarget.SetValue set, int order)
        {
            return new PropertyEditTarget(path, get, set)
            {
                DisplayName = path,
                Editor = editor,
                MemberSortOrder = order,
            };
        }
    }

    // ===========[ Wrapped-Mode matrix source for PGShowIf repro ]=====================
    // Reproduces the shape where an enum-like property lives behind a wrapper that
    // raises INPC on its Value (the outer source never fires), several PGShowIf
    // predicates read Mode.Value, and the rows must re-evaluate visibility without
    // rebuilding the grid.

    public enum eMatrixMode { A, B, C }

    public class WrappedModeValue<T> : NotifyPropertyChanged
    {
        private T m_value;

        [PGElevateAsParent(deferPGAttributesToParent: true)]
        public T Value
        {
            get => m_value;
            set => this.SetAndRaiseIfChanged(ref m_value, value);
        }

        public WrappedModeValue () { }
        public WrappedModeValue (T value) { m_value = value; }
    }

    public class WrappedModeMatrixSource
    {
        public WrappedModeValue<eMatrixMode> Mode { get; set; } = new WrappedModeValue<eMatrixMode>(eMatrixMode.A);

        [PGGroup("GroupA")]
        [PGShowIf(nameof(IsModeA))]
        public WrappedModeValue<string> A_GroupedColor { get; set; } = new WrappedModeValue<string>("#FF0000");

        [PGShowIf(nameof(IsModeB))]
        public WrappedModeValue<string> B_UngroupedColor { get; set; } = new WrappedModeValue<string>("#00FF00");

        [PGEditor("Single")]
        [PGEditContextBuilder("PG-Limits", "{ Min: 0.0, Max: 1.0, Step: 0.05 }")]
        [PGShowIf(nameof(IsModeB))]
        public WrappedModeValue<float> B_WithEditCtx { get; set; } = new WrappedModeValue<float>(0.5f);

        [PGShowIf(nameof(IsModeC))]
        public WrappedModeValue<string> C_First { get; set; } = new WrappedModeValue<string>("#0000FF");

        [PGShowIf(nameof(IsModeC))]
        public WrappedModeValue<string> C_Second { get; set; } = new WrappedModeValue<string>("#FFFF00");

        private bool IsModeA () => this.Mode.Value == eMatrixMode.A;
        private bool IsModeB () => this.Mode.Value == eMatrixMode.B;
        private bool IsModeC () => this.Mode.Value == eMatrixMode.C;
    }
}
