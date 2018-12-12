using System.Windows.Input;

namespace Microsoft.Research.DynamicDataDisplay
{
	// TODO: probably optimize memory usage by replacing implicit creation of 
	// all commands on first usage of this class - 
	// create each command on first access directly to it.

	/// <summary>Common chart plotter commands</summary>
	public static class ChartCommands
	{
		#region Auxiliary code for creation of commands

		private static RoutedUICommand CreateCommand(string name)
		{
			return new RoutedUICommand(name, name, typeof(ChartCommands));
		}

		private static RoutedUICommand CreateCommand(string name, params Key[] keys)
		{
			InputGestureCollection gestures = new InputGestureCollection();
			foreach (var key in keys)
			{
				gestures.Add(new KeyGesture(key));    
			}
			return new RoutedUICommand(name, name, typeof(ChartCommands), gestures);
		}

		private static RoutedUICommand CreateCommand(string name, MouseAction mouseAction) {
			return new RoutedUICommand(name, name, typeof(ChartCommands), new InputGestureCollection { new MouseGesture(mouseAction) });
		}

		private static RoutedUICommand CreateCommand(string name, params InputGesture[] gestures)
		{
			return new RoutedUICommand(name, name, typeof(ChartCommands), new InputGestureCollection(gestures));
		}

		#endregion

        //private static readonly RoutedUICommand zoomOutToMouse = CreateCommand("ZoomOutToMouse", MouseAction.RightDoubleClick); //leon bug 15112
        private static readonly RoutedUICommand zoomOutToMouse = CreateCommand("ZoomOutToMouse");
		/// <summary>
		/// Gets the value that represents the Zoom Out To Mouse command.
		/// </summary>
		public static RoutedUICommand ZoomOutToMouse
		{
			get { return ChartCommands.zoomOutToMouse; }
		}

        //private static readonly RoutedUICommand zoomInToMouse = CreateCommand("ZoomInToMouse", MouseAction.LeftDoubleClick); //leon bug 15112
        private static readonly RoutedUICommand zoomInToMouse = CreateCommand("ZoomInToMouse");
		/// <summary>
		/// Gets the value that represents the zoom in to mouse command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand ZoomInToMouse
		{
			get { return ChartCommands.zoomInToMouse; }
		} 

		private static readonly RoutedUICommand zoomWithParam = CreateCommand("ZoomWithParam");
		/// <summary>
		/// Gets the value that represents the zoom with parameter command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand ZoomWithParameter
		{
			get { return zoomWithParam; }
		}

        //private static readonly RoutedUICommand zoomIn = CreateCommand("ZoomIn", Key.OemPlus); //leon bug 15112
        private static readonly RoutedUICommand zoomIn = CreateCommand("ZoomIn");
		/// <summary>
		/// Gets the value that represents the zoom in command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand ZoomIn
		{
			get { return zoomIn; }
		}

        //private static readonly RoutedUICommand zoomOut = CreateCommand("ZoomOut", Key.OemMinus); //leon bug 15112
        private static readonly RoutedUICommand zoomOut = CreateCommand("ZoomOut");
		/// <summary>
		/// Gets the value that represents the zoom out command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand ZoomOut
		{
			get { return zoomOut; }
		}

        //private static readonly RoutedUICommand fitToView = CreateCommand("FitToView", Key.Home); //leon bug 15112
        private static readonly RoutedUICommand fitToView = CreateCommand("FitToView");
		/// <summary>
		/// Gets the value that represents the fit to view command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand FitToView
		{
			get { return ChartCommands.fitToView; }
		}

        //private static readonly RoutedUICommand scrollLeft = CreateCommand("ScrollLeft", Key.Left); //leon bug 15112
        private static readonly RoutedUICommand scrollLeft = CreateCommand("ScrollLeft");
		/// <summary>
		/// Gets the value that represents the scroll left command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand ScrollLeft
		{
			get { return ChartCommands.scrollLeft; }
		}

        //private static readonly RoutedUICommand scrollRight = CreateCommand("ScrollRight", Key.Right); //leon bug 15112
        private static readonly RoutedUICommand scrollRight = CreateCommand("ScrollRight");
		/// <summary>
		/// Gets the value that represents the scroll right command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand ScrollRight
		{
			get { return ChartCommands.scrollRight; }
		}

        //private static readonly RoutedUICommand scrollUp = CreateCommand("ScrollUp", Key.Up); //leon bug 15112
        private static readonly RoutedUICommand scrollUp = CreateCommand("ScrollUp");
		/// <summary>
		/// Gets the value that represents the scroll up command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand ScrollUp
		{
			get { return ChartCommands.scrollUp; }
		}

        //private static readonly RoutedUICommand scrollDown = CreateCommand("ScrollDown", Key.Down); //leon bug 15112
        private static readonly RoutedUICommand scrollDown = CreateCommand("ScrollDown");
		/// <summary>
		/// Gets the value that represents the scroll down command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand ScrollDown
		{
			get { return ChartCommands.scrollDown; }
		}

        //private static readonly RoutedUICommand saveScreenshot = CreateCommand("SaveScreenshot", new KeyGesture(Key.S, ModifierKeys.Control)); //leon bug 15112
        private static readonly RoutedUICommand saveScreenshot = CreateCommand("SaveScreenshot");
		/// <summary>
		/// Gets the value that represents the save screenshot command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand SaveScreenshot
		{
			get { return ChartCommands.saveScreenshot; }
		}

        //private static readonly RoutedUICommand copyScreenshot = CreateCommand("CopyScreenshot", Key.F11); //leon bug 15112
        private static readonly RoutedUICommand copyScreenshot = CreateCommand("CopyScreenshot");
		/// <summary>
		/// Gets the value that represents the copy screenshot command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand CopyScreenshot
		{
			get { return ChartCommands.copyScreenshot; }
		}

        //private static readonly RoutedUICommand showHelp = CreateCommand("ShowHelp", Key.F1); //leon bug 15112
        private static readonly RoutedUICommand showHelp = CreateCommand("ShowHelp");
		/// <summary>
		/// Gets the value that represents the show help command.
		/// </summary>
		/// <value></value>
		public static RoutedUICommand ShowHelp
		{
			get { return ChartCommands.showHelp; }
		}

        //private static readonly RoutedUICommand showHelp = CreateCommand("ShowHelp", Key.F1); //leon bug 15112
        private static readonly RoutedUICommand isShowLegend = CreateCommand("isShowLegend");
        /// <summary>
        /// Gets the value that represents the show help command.
        /// </summary>
        /// <value></value>
        public static RoutedUICommand IsShowLegend
        {
            get { return ChartCommands.isShowLegend; }
        }
	}
}
