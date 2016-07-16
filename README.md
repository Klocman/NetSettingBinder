# NetSettingBinder
A tool for binding controls, variables and typed event handlers to custom Settings classes.

## Building
To build this project you can use any modern version of Visual Studio. There are no dependencies outside of the .NET Framework itself. The lowest supported framework version is 3.5.

## Introduction

If you have ever wrote a Windows Forms application, you most likely faced the built-in Settings system. It's got a pretty nice editor, is statically typed, and you can even extend it by using custom providers. Nice and clean. That is, until you try to hook it up to your controls. At first you might favor simple event handlers, but as the code-behind grows it all becomes hard to maintain.

There are two common ways to manage settings:
*   Forms with OK/Apply/Cancel style buttons (used extensively in Windows)
*   Controls that instantly update the setting they are bound to

This project focuses on the latter.

## A simple example
![Alt text](https://raw.githubusercontent.com/Klocman/NetSettingBinder/master/Example.PNG)

This is all* of the code-behind necessary. The Checkbox.Checked and GroupBox.Enabled are bound to the setting "Checkbox", while the Label and the TextBox are bound to a setting "TextBox". 
In case you are wondering, the Binder property is added like this:

    internal sealed partial class Settings
    {
    	public Settings()
    	{
    		Binder = new SettingBinder<Settings>(this);
    	}
    
    	public SettingBinder<Settings> Binder { get; }
    }

This example project is available in the repository.

## A more complex example
![Alt text](https://raw.githubusercontent.com/Klocman/NetSettingBinder/master/Example2.jpg)

Here you can see a part of the sidebar from [BCUninstaller](http://klocmansoftware.weebly.com/), one of my projects. Ordinarily you would expect a whole lot of event handlers in the code-behind, writing to my Settings class, and then an event handler used to read back from the settings (in case they get changed from somewhere else).

Receiving updates the bigger problem out of the two, as you don't get XyzChanged events for your settings, only the ApplicationSettingsBase.PropertyChanged.
Why is this a problem? It's because you receive a name of the changed property instead of its reference (since properties saved in settings are generally value types). You have to put the name of the needed property in a string and compare it in the event handler to filter out other properties. These days you can use name(ClassName), but it can still be problematic.

Did you take a good look at the control above? Take a guess of how much code is needed to back it up. Done? This is how the code-behind looks like:

    public partial class PropertiesSidebar : UserControl
    {
        public PropertiesSidebar()
        {
            InitializeComponent();
    
            var settings = Settings.Default.SettingBinder;
    
            settings.BindControl(checkBoxViewCheckboxes, x => x.UninstallerListUseCheckboxes, this);
            settings.BindControl(checkBoxViewGroups, x => x.UninstallerListUseGroups, this);
    
            settings.BindControl(checkBoxListHideMicrosoft, x => x.FilterHideMicrosoft, this);
            settings.BindControl(checkBoxShowUpdates, x => x.FilterShowUpdates, this);
            settings.BindControl(checkBoxListSysComp, x => x.FilterShowSystemComponents, this);
            settings.BindControl(checkBoxListProtected, x => x.FilterShowProtected, this);
    
            settings.BindControl(checkBoxBatchSortQuiet, x => x.AdvancedIntelligentUninstallerSorting, this);
            settings.BindControl(checkBoxInvalidTest, x => x.AdvancedTestInvalid, this);
            settings.BindControl(checkBoxCertTest, x => x.AdvancedTestCertificates, this);
            settings.BindControl(checkBoxDiisableProtection, x => x.AdvancedDisableProtection, this);
            settings.BindControl(checkBoxSimulate, x => x.AdvancedSimulate, this);
    
            settings.SendUpdates(this);
            Disposed += (x, y) => settings.RemoveHandlers(this);
        }
    }

This is all there is to it, nothing fancy has been done in the designer. It's almost as good as it could be in WPF, but I'm not complaining!

## How does this work?
Alright then, let's see how this thing works. This is how BindControl is implemented:

		public void BindControl(TextBox sourceControl, Expression<Func<TSettingClass, string>> targetSetting, object tag)
		{
				Bind(x => sourceControl.Text = x, () => sourceControl.Text,
						eh => sourceControl.TextChanged += eh, eh => sourceControl.TextChanged -= eh,
						targetSetting, tag);
		}

Here is a short breakdown of the parameters:

*    sourceControl - The control you are binding to
*    TSettingClass - Type of your custom Settings class
*    This overload is used for binding text in TextBoxes, so the second type is a string
*    targetSetting - Lambda of style x=>x.Property, where x is the custom Settings class
*    tag - Object used to manage the binding.

The SendUpdates method forces the bindings to update the controls (they are not updated on creation of the bindings). As you might have guessed, RemoveHandlers removes those bindings.

As you can see, BindControl is just syntactic sugar for the Bind method. Here is a simplified implementation of the Bind method:

  	public void Bind<T>(Action<T> setter, Func<T> getter, Action<EventHandler> registerEvent, Action<EventHandler> unregisterEvent,	
  	  Expression<Func<TSettingClass, T>> targetSetting, object tag)
  	{
  			var memberSelectorExpression = targetSetting.Body as MemberExpression;
  			var property = memberSelectorExpression.Member as PropertyInfo;
  	
  			EventHandler checkedChanged = (x, y) => property.SetValue(_settingSet, getter(), null);
  	
  			registerEvent(checkedChanged);
  	
  			SettingChangedEventHandler<T> settingChanged = (x, y) =>
  			{
  					var remoteValue = getter();
  					if (!remoteValue.Equals(y.NewValue))
  					{
  							unregisterEvent(checkedChanged);
  							setter(y.NewValue);
  							registerEvent(checkedChanged);
  					}
  			};
  	
  			Subscribe(settingChanged, targetSetting, tag);
  	}

Parameters are pretty self explanatory, setter and getter are used to interface with the variable you want to bind. The registerEvent and unregisterEvent should subscribe to an event that fires when the variable is changed. T is the type of the variable. Let's see what's happening in this method.

*    First, PropertyInfo of the target setting is extracted from the lambda.
*    A new EventHandler delegate is created that will use this information to set a new value to the target setting. The value is obtained by the getter delegate.
*    It is then registered using registerEvent delegate. This is the control-side event handler.
*    Another event handler is created (this time it is a customized one, more on it below). If new value of the setting is different from the bound variable, it will update the variable with the new value. It is uses unregisterEvent to prevent an infinite loop.
*    Finally, the newly created custom event handler is registered using the Subscribe method.

Again, this method could be considered an syntactic sugar to the Subscribe method. Let's have a quick look at the custom event handler:

  public delegate void SettingChangedEventHandler<TProperty>(object sender, 
      SettingChangedEventArgs<TProperty> args);
  
  	public sealed class SettingChangedEventArgs<T> : EventArgs
  	{
  		internal SettingChangedEventArgs(T value)
  		{
  			NewValue = value;
  		}
  	
  		public T NewValue
  		{
  			get; private set;
  		}
  	}

Pretty simple. Most notably the type of the property is preserved. Next up, the Subscribe method and related items: (almost there!)

	private readonly List<KeyValuePair<string, ISettingChangedHandlerEntry>> 
		_eventEntries;
	
	public void Subscribe<TProperty>(SettingChangedEventHandler<TProperty> handler,
				Expression<Func<TSettingClass, TProperty>> targetProperty, object tag)
	{
		var memberSelectorExpression = targetProperty.Body as MemberExpression;
		var name = memberSelectorExpression.Member.Name;
	
		_eventEntries.Add(new KeyValuePair<string, ISettingChangedHandlerEntry>(name,
			new SettingChangedHandlerEntry<TProperty>(handler, tag)));
	}
	
	private interface ISettingChangedHandlerEntry
	{
		object Tag { get; set; }
	
		void SendEvent(object value);
	}
	
	private sealed class SettingChangedHandlerEntry<T> : ISettingChangedHandlerEntry
	{
		internal SettingChangedHandlerEntry(SettingChangedEventHandler<T> handler,
			object tag)
		{
			Handler = handler;
			Tag = tag;
		}
	
		public object Tag { get; set; }
	
		private SettingChangedEventHandler<T> Handler { get; set; }
	
		/// <summary>
		/// Implemented explicitly to hide it from outside access
		/// </summary>
		void ISettingChangedHandlerEntry.SendEvent(object value)
		{
			Handler(this, new SettingChangedEventArgs<T>((T)value));
		}
	}

The Subscribe method is the most basic one. It only takes an event handler and a property lambda. At last the property lambda is converted into a string containing the target setting's name. With this information a new binding is created and added to the binding list.

Because SettingChangedHandlerEntry is a generic, unknown type, it is impossible to create a generic list out of it. This can be overcome by inheriting from a non-generic base class, or by implementing an interface. In this case the interface ISettingChangedHandlerEntry is used to store the handlers into a single list.

As a side note, if you plan on having very large amounts of settings it might be wise to change the List into a Dictionary and use Lists as values to improve look-up performance. The type would probably look like this: Dictionary<string, List<ISettingChangedHandlerEntry>>.

This is all there is to creating a one-way control binding. If you change the value of your control, the corresponding setting will now update. Actually the last step was not even required for that. What it was required for, was binding the setting changes back to the control. Here is the code for that:

	private void PropertyChangedCallback(object sender, PropertyChangedEventArgs e)
	{
		foreach (var entry in _eventEntries)
		{
			if (entry.Key.Equals(e.PropertyName))
			{
				entry.Value.SendEvent(_settingSet[e.PropertyName]);
			}
		}
	}

This handler is hooked up to the PropertyChanged event of your custom Settings class. As you can see it scans the binding list for entries with matching property names and executes the ISettingChangedHandlerEntry.SendEvent methods on them.

You can read the code snippets backwards to see the path that the event will take to reach our starting control.

Finally let's check out the two methods I mentioned at the beginning - RemoveHandlers and SendUpdates. Here are their implementations:

	public void RemoveHandlers(object groupTag)
	{
		_eventEntries.RemoveAll(pair => pair.Value.Tag.Equals(groupTag));
	}
	
	public void SendUpdates(object groupTag)
	{
		foreach (var entry in _eventEntries)
		{
			if (entry.Value.Tag != null && entry.Value.Tag.Equals(groupTag))
			{
				entry.Value.SendEvent(_settingSet[entry.Key]);
			}
		}
	}

It doesn't get any simpler than that. To remove the bindings I used an extension method from Linq namespace to make the code simpler (and faster to write).

This is about it, check out the source code to see how all of this is connected together. Thanks to everyone who have read all of this, I didn't think anyone would :)
