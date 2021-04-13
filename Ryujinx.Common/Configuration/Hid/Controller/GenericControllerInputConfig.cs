﻿namespace Ryujinx.Common.Configuration.Hid.Controller
{
    public class GenericControllerInputConfig<Button, Stick> : GenericInputConfigurationCommon<Button, Stick> where Button : unmanaged where Stick : unmanaged
    {
        /// <summary>
        /// Left JoyCon Controller Stick Bindings
        /// </summary>
        public JoyconConfigControllerStick<Button, Stick> LeftJoyconStick;

        /// <summary>
        /// Right JoyCon Controller Stick Bindings
        /// </summary>
        public JoyconConfigControllerStick<Button, Stick> RightJoyconStick;

        /// <summary>
        /// Controller Left Analog Stick Deadzone
        /// </summary>
        public float DeadzoneLeft { get; set; }

        /// <summary>
        /// Controller Right Analog Stick Deadzone
        /// </summary>
        public float DeadzoneRight { get; set; }

        /// <summary>
        /// Controller Trigger Threshold
        /// </summary>
        public float TriggerThreshold { get; set; }
    }
}
