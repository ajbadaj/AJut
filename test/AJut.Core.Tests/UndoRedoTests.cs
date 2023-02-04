namespace AJut.Core.UnitTests
{
    using AJut.MathUtilities;
    using AJut.UndoRedo;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Linq;

    [TestClass]
    public class UndoRedoTests
    {
        [TestMethod]
        public void UndoRedo_ProofOfConcept_SmokeTest ()
        {
            var manager = new UndoRedoManager();

            int value = 3;

            Assert.IsFalse(manager.AnyUndos);
            Assert.IsFalse(manager.AnyRedos);
            Assert.AreEqual(0, manager.UndoStack.Count);
            Assert.AreEqual(0, manager.RedoStack.Count);
            manager.ExecuteAction(new LambdaUndoableAction("Multiply x2", () => value *= 2, () => value /= 2, "MATH"));
            Assert.AreEqual(6, value);
            Assert.IsTrue(manager.AnyUndos);
            Assert.IsFalse(manager.AnyRedos);
            Assert.AreEqual(1, manager.UndoStack.Count);
            Assert.AreEqual(0, manager.RedoStack.Count);

            manager.ExecuteAction(new LambdaUndoableAction("Multiply x4", () => value *= 4, () => value /= 4, "MATH"));
            Assert.AreEqual(24, value);
            Assert.IsTrue(manager.AnyUndos);
            Assert.IsFalse(manager.AnyRedos);
            Assert.AreEqual(2, manager.UndoStack.Count);
            Assert.AreEqual(0, manager.RedoStack.Count);

            Assert.IsTrue(manager.Undo());
            Assert.AreEqual(6, value);
            Assert.IsTrue(manager.AnyUndos);
            Assert.IsTrue(manager.AnyRedos);
            Assert.AreEqual(1, manager.UndoStack.Count);
            Assert.AreEqual(1, manager.RedoStack.Count);

            Assert.IsTrue(manager.Undo());
            Assert.AreEqual(3, value);
            Assert.IsFalse(manager.AnyUndos);
            Assert.IsTrue(manager.AnyRedos);
            Assert.AreEqual(0, manager.UndoStack.Count);
            Assert.AreEqual(2, manager.RedoStack.Count);

            Assert.IsTrue(manager.Redo());
            Assert.AreEqual(6, value);
            Assert.IsTrue(manager.AnyUndos);
            Assert.IsTrue(manager.AnyRedos);
            Assert.AreEqual(1, manager.UndoStack.Count);
            Assert.AreEqual(1, manager.RedoStack.Count);
        }

        [TestMethod]
        public void UndoRedo_GroupTest ()
        {
            var manager = new UndoRedoManager();

            int value = 9;

            var groupAction = new UndoableGroupAction("Lots of math", "MATH");
            groupAction.Add(new LambdaUndoableAction("Add 3", () => value += 3, () => value -= 3));
            groupAction.Add(new LambdaUndoableAction("Div 2", () => value /= 2, () => value *= 2));


            Assert.IsFalse(manager.AnyUndos);
            Assert.IsFalse(manager.AnyRedos);
            Assert.AreEqual(0, manager.UndoStack.Count);
            Assert.AreEqual(0, manager.RedoStack.Count);

            manager.ExecuteAction(groupAction);
            Assert.AreEqual(6, value);
            Assert.IsTrue(manager.AnyUndos);
            Assert.IsFalse(manager.AnyRedos);
            Assert.AreEqual(1, manager.UndoStack.Count);
            Assert.AreEqual(0, manager.RedoStack.Count);

            Assert.IsTrue(manager.Undo());
            Assert.AreEqual(9, value);
            Assert.IsFalse(manager.AnyUndos);
            Assert.IsTrue(manager.AnyRedos);
            Assert.AreEqual(0, manager.UndoStack.Count);
            Assert.AreEqual(1, manager.RedoStack.Count);

            Assert.IsTrue(manager.Redo());
            Assert.AreEqual(6, value);
            Assert.IsTrue(manager.AnyUndos);
            Assert.IsFalse(manager.AnyRedos);
            Assert.AreEqual(1, manager.UndoStack.Count);
            Assert.AreEqual(0, manager.RedoStack.Count);
        }


        [TestMethod]
        public void UndoRedo_SubstackTest ()
        {
            var undoredo = new UndoRedoManager();

            int value = 9;

            //manager.StartCaptureGroup();
            var substackId = undoredo.CreateSubsidiaryStack();
            UndoRedoManager undoredoSubstack = undoredo.GetSubstack(substackId);
            undoredo.ExecuteAction(substackId, new LambdaUndoableAction("Add 3", () => value += 3, () => value -= 3));
            undoredo.ExecuteAction(substackId, new LambdaUndoableAction("Div 2", () => value /= 2, () => value *= 2));
            Assert.AreEqual(6, value);

            // main stack
            Assert.IsFalse(undoredo.AnyUndos);
            Assert.IsFalse(undoredo.AnyRedos);
            Assert.AreEqual(0, undoredo.UndoStack.Count);
            Assert.AreEqual(0, undoredo.RedoStack.Count);

            // substack
            Assert.IsTrue(undoredoSubstack.AnyUndos);
            Assert.AreEqual(2, undoredoSubstack.UndoStack.Count);

            undoredo.CommitSubstack(substackId, "Lots of math", "MATH");

            // main stack
            Assert.IsTrue(undoredo.AnyUndos);
            Assert.IsFalse(undoredo.AnyRedos);
            Assert.AreEqual(1, undoredo.UndoStack.Count);
            Assert.AreEqual(0, undoredo.RedoStack.Count);

            // Undo the whole substack
            Assert.IsTrue(undoredo.Undo());
            Assert.AreEqual(9, value);
            Assert.IsFalse(undoredo.AnyUndos);
            Assert.IsTrue(undoredo.AnyRedos);
            Assert.AreEqual(0, undoredo.UndoStack.Count);
            Assert.AreEqual(1, undoredo.RedoStack.Count);

            Assert.IsTrue(undoredo.Redo());
            Assert.AreEqual(6, value);
            Assert.IsTrue(undoredo.AnyUndos);
            Assert.IsFalse(undoredo.AnyRedos);
            Assert.AreEqual(1, undoredo.UndoStack.Count);
            Assert.AreEqual(0, undoredo.RedoStack.Count);
        }

        [TestMethod]
        public void UndoRedo_GroupTest_EmptyGroupsAreNotAdded ()
        {
            var manager = new UndoRedoManager();
            manager.ExecuteAction(new UndoableGroupAction("w/e"));
            Assert.IsFalse(manager.AnyUndos);
            Assert.IsFalse(manager.AnyRedos);

            manager.AddAction(new UndoableGroupAction("w/e"));
            Assert.IsFalse(manager.AnyUndos);
            Assert.IsFalse(manager.AnyRedos);
        }

        [TestMethod]
        public void UndoRedo_GroupTest_SingleGroupItemCollapses ()
        {
            var undoredo = new UndoRedoManager();

            var group = new UndoableGroupAction("w/e");
            group.Add(new LambdaUndoableAction("Nothing", () => { }, () => { }));
            undoredo.ExecuteAction(group);
            Assert.IsTrue(undoredo.AnyUndos);
            Assert.AreEqual(1, undoredo.UndoStack.Count);
            Assert.AreEqual("Nothing", undoredo.UndoStack.First().DisplayName);
        }

        [TestMethod]
        public void UndoRedo_SubstackOverride_BasicUsage ()
        {
            var undoRedo = new UndoRedoManager();
            int mathTarget = 10;

            // Stock the current undo/redo with an undoable
            undoRedo.ExecuteAction(new LambdaUndoableAction("SimpleMath", () => mathTarget += 5, () => mathTarget -= 5));
            Assert.AreEqual(15, mathTarget);
            Assert.AreEqual(1, undoRedo.UndoStack.Count);
            Assert.AreEqual(0, undoRedo.RedoStack.Count);

            // Create the substack and set it as 
            UndoRedoManager.SubstackOverride overrideSubstack = undoRedo.OverrideUndoRedoCallsToUseNewSubstack();
            Assert.IsNotNull(overrideSubstack);
            Assert.IsTrue(overrideSubstack.IsActive);

            undoRedo.ExecuteAction(new LambdaUndoableAction("SimpleMath", () => mathTarget += 7, () => mathTarget -= 7));
            Assert.AreEqual(22, mathTarget);
            Assert.AreEqual(1, undoRedo.UndoStack.Count);
            Assert.AreEqual(0, undoRedo.RedoStack.Count);
            Assert.AreEqual(1, undoRedo.GetSubstack(overrideSubstack.Id).UndoStack.Count);
            Assert.AreEqual(0, undoRedo.GetSubstack(overrideSubstack.Id).RedoStack.Count);

            undoRedo.ExecuteAction(new LambdaUndoableAction("SimpleMath", () => mathTarget += 10, () => mathTarget -= 10));
            Assert.AreEqual(32, mathTarget);
            Assert.AreEqual(1, undoRedo.UndoStack.Count);
            Assert.AreEqual(0, undoRedo.RedoStack.Count);
            Assert.AreEqual(2, undoRedo.GetSubstack(overrideSubstack.Id).UndoStack.Count);
            Assert.AreEqual(0, undoRedo.GetSubstack(overrideSubstack.Id).RedoStack.Count);

            // Undo, this should undo something done on the substack - not on the main stack!
            Assert.IsTrue(undoRedo.Undo());
            Assert.AreEqual(22, mathTarget);
            Assert.AreEqual(1, undoRedo.UndoStack.Count);
            Assert.AreEqual(0, undoRedo.RedoStack.Count);
            Assert.AreEqual(1, undoRedo.GetSubstack(overrideSubstack.Id).UndoStack.Count);
            Assert.AreEqual(1, undoRedo.GetSubstack(overrideSubstack.Id).RedoStack.Count);

            // Redo, this should redo something done on the substack - not on the main stack!
            Assert.IsTrue(undoRedo.Redo());
            Assert.AreEqual(32, mathTarget);
            Assert.AreEqual(1, undoRedo.UndoStack.Count);
            Assert.AreEqual(0, undoRedo.RedoStack.Count);
            Assert.AreEqual(2, undoRedo.GetSubstack(overrideSubstack.Id).UndoStack.Count);
            Assert.AreEqual(0, undoRedo.GetSubstack(overrideSubstack.Id).RedoStack.Count);

            // Commit the substack, this should now add the second entry to the main stack as a grouped entry
            Assert.IsTrue(overrideSubstack.CommitAndEndOverride("Grouped Math"));
            Assert.AreEqual(32, mathTarget);
            Assert.AreEqual(2, undoRedo.UndoStack.Count);
            Assert.AreEqual(0, undoRedo.RedoStack.Count);

            // Undo should now undo the group entry
            Assert.IsTrue(undoRedo.Undo());
            Assert.AreEqual(15, mathTarget);
            Assert.AreEqual(1, undoRedo.UndoStack.Count);
            Assert.AreEqual(1, undoRedo.RedoStack.Count);

            // Undo should now undo the first entry that was made directly to the main stack
            Assert.IsTrue(undoRedo.Undo());
            Assert.AreEqual(10, mathTarget);
            Assert.AreEqual(0, undoRedo.UndoStack.Count);
            Assert.AreEqual(2, undoRedo.RedoStack.Count);
            Assert.IsFalse(undoRedo.Undo());

            // Redo should now put the main stack to how it started with the first entry
            Assert.IsTrue(undoRedo.Redo());
            Assert.AreEqual(15, mathTarget);
            Assert.AreEqual(1, undoRedo.UndoStack.Count);
            Assert.AreEqual(1, undoRedo.RedoStack.Count);

            // Redo should now apply the group that represents the comitted substack
            Assert.IsTrue(undoRedo.Redo());
            Assert.AreEqual(32, mathTarget);
            Assert.AreEqual(2, undoRedo.UndoStack.Count);
            Assert.AreEqual(0, undoRedo.RedoStack.Count);
            Assert.IsFalse(undoRedo.Redo());
        }
    }
}
