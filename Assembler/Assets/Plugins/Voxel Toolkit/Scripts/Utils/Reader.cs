using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
	/// <summary>
	/// Helper class that is used to read the voxel file
	/// </summary>
	public class Reader
	{
		private readonly BinaryReader reader;

		public long Position
		{
			get => reader.BaseStream.Position;
			set => reader.BaseStream.Position = value;
		}

		public bool HasSomethingToRead => reader.BaseStream.Position < reader.BaseStream.Length; 

		public Reader(BinaryReader reader)
		{
			this.reader = reader;
		}

		public void ConsumeCharacter(char value)
		{
			var character = reader.ReadChar();
			if (character != value)
				throw new VoxelDataReadException("Expected " + value + " but got " + character);
		}

		public int ConsumeInt()
		{
			return reader.ReadInt32();
		}
        
		public uint ConsumeUint()
		{
			return reader.ReadUInt32();
		}

		public Vector3Int ConsumeVector3IntInt32()
		{
			var x = reader.ReadInt32();
			var y = reader.ReadInt32();
			var z = reader.ReadInt32();
			return new Vector3Int(x, y, z);
		}
        
		public Vector3Int ConsumeVector3IntByte()
		{
			var x = reader.ReadByte();
			var y = reader.ReadByte();
			var z = reader.ReadByte();
			return new Vector3Int(x, y, z);
		}

		public byte ConsumeByte()
		{
			return reader.ReadByte();
		}

		public void Skip(int bytes)
		{
			reader.BaseStream.Position += bytes;
		}
        
		public void ConsumeString(string stringToConsume)
		{
			var startingPosition = reader.BaseStream.Position;
			try
			{
				foreach (var character in stringToConsume)
					ConsumeCharacter(character);
			}
			catch (Exception exception)
			{
				reader.BaseStream.Position = startingPosition;
				var resultingString = new string(reader.ReadChars((int)math.min(stringToConsume.Length, reader.BaseStream.Length - reader.BaseStream.Position)));
				Debug.LogError($"Tried to consume string {stringToConsume} but got {resultingString}");
				Debug.LogException(exception);
				throw exception;
			}
		}

		public string ConsumeString()
		{
			var size = ConsumeInt();
			var builder = new StringBuilder();
			for (var index = 0; index < size; index++)
			{
				var character = (char)ConsumeByte();
				builder.Append(character);
			}

			return builder.ToString();
		}

		public Dictionary<string, string> ConsumeDictionary()
		{
			var pairsCount = ConsumeInt();
			var result = new Dictionary<string, string>();

			for (var index = 0; index < pairsCount; index++)
			{
				var key = ConsumeString();
				var value = ConsumeString();
				result.Add(key, value);
			}

			return result;
		}

		public bool TryConsumeString(string expectedString)
		{
			var startPosition = Position;
			if (Position >= reader.BaseStream.Length)
				return false;
            
			var consumed = ConsumeString(expectedString.Length);
			if (consumed != expectedString)
				Position = startPosition;

			return Position != startPosition;
		}

		public string ConsumeString(int length)
		{
			var builder = new StringBuilder();
			for (var index = 0; index < length; index++)
			{
				var character = reader.ReadChar();
				if (character == '\0')
					throw new VoxelDataReadException($"Expected string of length {length} but encounter '\\0' terminator early");

				builder.Append(character);
			}

			return builder.ToString();
		}
	}
}