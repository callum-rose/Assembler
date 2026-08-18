using System;
using UnityEngine;

namespace ES3Types
{
	[UnityEngine.Scripting.Preserve]
	[ES3PropertiesAttribute()]
	public class ES3UserType_FloatField : ES3ObjectType
	{
		public static ES3Type Instance = null;

		public ES3UserType_FloatField() : base(typeof(UnityEngine.Rendering.DebugUI.FloatField)){ Instance = this; priority = 1; }


		protected override void WriteObject(object obj, ES3Writer writer)
		{
			var instance = (UnityEngine.Rendering.DebugUI.FloatField)obj;
			
		}

		protected override void ReadObject<T>(ES3Reader reader, object obj)
		{
			var instance = (UnityEngine.Rendering.DebugUI.FloatField)obj;
			foreach(string propertyName in reader.Properties)
			{
				switch(propertyName)
				{
					
					default:
						reader.Skip();
						break;
				}
			}
		}

		protected override object ReadObject<T>(ES3Reader reader)
		{
			var instance = new UnityEngine.Rendering.DebugUI.FloatField();
			ReadObject<T>(reader, instance);
			return instance;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetStaticVariables()
		{
			Instance = null;
		}
	}


	public class ES3UserType_FloatFieldArray : ES3ArrayType
	{
		public static ES3Type Instance;

		public ES3UserType_FloatFieldArray() : base(typeof(UnityEngine.Rendering.DebugUI.FloatField[]), ES3UserType_FloatField.Instance)
		{
			Instance = this;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetStaticVariables()
		{
			Instance = null;
		}
	}
}