using System;
using System.ComponentModel;

namespace Refactor
{
	public abstract class BaseProperty : INotifyPropertyChanged
	{
		///<summary>
		/// Event that happens a property value changes.
		///</summary>
		public virtual event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Call this to inform dependents that a property changed.
		/// </summary>
		/// <param name="propertyName">The name of the changed property.</param>
		protected void OnPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}
	}
}