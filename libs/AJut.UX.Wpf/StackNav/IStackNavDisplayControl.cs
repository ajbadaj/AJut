namespace AJut.UX
{
    /// <summary>
    /// The interface a control needs to implement in order to be used as a display in the StackNav environment. Besides 
    /// implementing this interface, a StackNav display needs also to have an empty constructor.
    /// </summary>
    public interface IStackNavDisplayControl
    {
        /// <summary>
        /// Sets up the control with the adapter it uses to customize it's display and usage in a StackNav flow
        /// </summary>
        void Setup (StackNavAdapter adapter);

        /// <summary>
        /// Sets the state the <see cref="StackNavFlowController"/> was given for this display
        /// </summary>
        /// <param name="state"></param>
        void SetState (object state) { }

        /// <summary>
        /// Generates state for handoff when the display is covered so that the <see cref="StackNavFlowController"/> can re-create it properly when it's shown again
        /// </summary>
        object GenerateState () => null;
    }
}
