// using System;
// using UnityEngine;
//
// namespace Assembler.Variables
// {
// 	public abstract class GameVariable
// 	{
// 		public event Action Changed;
//
// 		protected void InvokeChanged()
// 		{
// 			Changed?.Invoke();
// 		}
// 	}
//
// 	public abstract class GameVariable<T> : GameVariable
// 	{
// 		[SerializeField] private T value;
//
// 		public T Value
// 		{
// 			get => value;
// 			set => SetValue(value);
// 		}
//
// 		private void SetValue(T newValue)
// 		{
// 			if (Equals(value, newValue))
// 			{
// 				return;
// 			}
//
// 			value = newValue;
// 			InvokeChanged();
// 		}
// 	}
//
// 	public class IntVariable : GameVariable<int> { }
//
// 	public class FloatVariable : GameVariable<float> { }
//
// 	public class BoolVariable : GameVariable<bool> { }
//
// 	public class StringVariable : GameVariable<string> { }
//
// 	public class Vector3Variable : GameVariable<Vector3> { }
// }