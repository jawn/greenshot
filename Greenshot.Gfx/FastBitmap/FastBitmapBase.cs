﻿#region Greenshot GNU General Public License

// Greenshot - a free and open source screenshot tool
// Copyright (C) 2007-2017 Thomas Braun, Jens Klingen, Robin Krom
// 
// For more information see: http://getgreenshot.org/
// The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 1 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#endregion

#region Usings

using System;
using System.Drawing;
using System.Drawing.Imaging;
using Dapplo.Windows.Common.Extensions;
using Dapplo.Windows.Common.Structs;

#endregion

namespace Greenshot.Gfx.FastBitmap
{
	/// <summary>
	///     The base class for the fast bitmap implementation
	/// </summary>
	public abstract unsafe class FastBitmapBase : IFastBitmapWithClip, IFastBitmapWithOffset
	{
		protected const int PixelformatIndexA = 3;
		protected const int PixelformatIndexR = 2;
		protected const int PixelformatIndexG = 1;
		protected const int PixelformatIndexB = 0;

		public const int ColorIndexR = 0;
		public const int ColorIndexG = 1;
		public const int ColorIndexB = 2;
		public const int ColorIndexA = 3;

		private int _left;

		private int _top;

		protected NativeRect Area;

		/// <summary>
		///     The bitmap for which the FastBitmap is creating access
		/// </summary>
		protected Bitmap Bitmap;

		protected bool BitsLocked;

		protected BitmapData BmData;
		protected byte* Pointer;
		protected int Stride; /* bytes per pixel row */

		/// <summary>
		///     Constructor which stores the image and locks it when called
		/// </summary>
		/// <param name="bitmap">Bitmap</param>
		/// <param name="area">NativeRect</param>
		protected FastBitmapBase(Bitmap bitmap, NativeRect? area = null)
		{
			Bitmap = bitmap;
			var bitmapArea = new NativeRect(NativePoint.Empty, bitmap.Size);
			Area = area?.Intersect(bitmapArea) ?? bitmapArea;
			// As the lock takes care that only the specified area is made available we need to calculate the offset
			Left = Area.Left;
			Top = Area.Top;
			// Default cliping is done to the area without invert
			Clip = Area;
			InvertClip = false;
			// Always lock, so we don't need to do this ourselves
			Lock();
		}

		/// <summary>
		///     If this is set to true, the bitmap will be disposed when disposing the IFastBitmap
		/// </summary>
		public bool NeedsDispose { get; set; }

		public NativeRect Clip { get; set; }

		public bool InvertClip { get; set; }

		public void SetResolution(float horizontal, float vertical)
		{
			Bitmap.SetResolution(horizontal, vertical);
		}

		/// <summary>
		///     Return the size of the image
		/// </summary>
		public Size Size
		{
			get
			{
				if (Area == NativeRect.Empty)
				{
					return Bitmap.Size;
				}
				return Area.Size;
			}
		}

		/// <summary>
		///     Return the width of the image
		/// </summary>
		public int Width
		{
			get
			{
				if (Area == NativeRect.Empty)
				{
					return Bitmap.Width;
				}
				return Area.Width;
			}
		}

		/// <summary>
		///     Return the height of the image
		/// </summary>
		public int Height
		{
			get
			{
				if (Area == NativeRect.Empty)
				{
					return Bitmap.Height;
				}
				return Area.Height;
			}
		}

		/// <summary>
		///     Return the left of the fastbitmap, this is also used as an offset
		/// </summary>
		public int Left
		{
			get { return 0; }
			set { _left = value; }
		}

		/// <summary>
		///     Return the top of the fastbitmap, this is also used as an offset
		/// </summary>
		public int Top
		{
			get { return 0; }
			set { _top = value; }
		}

		/// <summary>
		///     Return the right of the fastbitmap
		/// </summary>
		public int Right => Left + Width;

		/// <summary>
		///     Return the bottom of the fastbitmap
		/// </summary>
		public int Bottom => Top + Height;

		/// <summary>
		///     Returns the underlying bitmap, unlocks it and prevents that it will be disposed
		/// </summary>
		public Bitmap UnlockAndReturnBitmap()
		{
			if (BitsLocked)
			{
				Unlock();
			}
			NeedsDispose = false;
			return Bitmap;
		}

		public virtual bool HasAlphaChannel => false;

		/// <summary>
		///     The public accessible Dispose
		///     Will call the GarbageCollector to SuppressFinalize, preventing being cleaned twice
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		///     Lock the bitmap so we have direct access to the memory
		/// </summary>
		public void Lock()
		{
			if (Width <= 0 || Height <= 0 || BitsLocked)
			{
				return;
			}
			BmData = Bitmap.LockBits(Area, ImageLockMode.ReadWrite, Bitmap.PixelFormat);
			BitsLocked = true;

			var scan0 = BmData.Scan0;
			Pointer = (byte*) (void*) scan0;
			Stride = BmData.Stride;
		}

		/// <summary>
		///     Unlock the System Memory
		/// </summary>
		public void Unlock()
		{
			if (BitsLocked)
			{
				Bitmap.UnlockBits(BmData);
				BitsLocked = false;
			}
		}

		/// <summary>
		///     Draw the stored bitmap to the destionation bitmap at the supplied point
		/// </summary>
		/// <param name="graphics"></param>
		/// <param name="destination"></param>
		public void DrawTo(Graphics graphics, NativePoint destination)
		{
			DrawTo(graphics, new NativeRect(destination, Area.Size));
		}

		/// <summary>
		///     Draw the stored Bitmap on the Destination bitmap with the specified rectangle
		///     Be aware that the stored bitmap will be resized to the specified rectangle!!
		/// </summary>
		/// <param name="graphics"></param>
		/// <param name="destinationRect"></param>
		public void DrawTo(Graphics graphics, NativeRect destinationRect)
		{
			// Make sure this.bitmap is unlocked, if it was locked
			var isLocked = BitsLocked;
			if (isLocked)
			{
				Unlock();
			}

			graphics.DrawImage(Bitmap, destinationRect, Area, GraphicsUnit.Pixel);
		}

		/// <summary>
		///     returns true if x & y are inside the FastBitmap
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns>true if x & y are inside the FastBitmap</returns>
		public bool Contains(int x, int y)
		{
			return Area.Contains(x - Left, y - Top);
		}

		/// <summary>
		///     Retrieve the color at x,y
		/// </summary>
		/// <param name="x">int x</param>
		/// <param name="y">int y</param>
		/// <returns>Color</returns>
		public abstract Color GetColorAt(int x, int y);

		/// <summary>
		///     Set the color at x,y
		/// </summary>
		/// <param name="x">int x</param>
		/// <param name="y">int y</param>
		/// <param name="color">Color</param>
		public abstract void SetColorAt(int x, int y, ref Color color);

        /// <summary>
        ///     Retrieve the color at x,y as byte[]
        /// </summary>
        /// <param name="x">int x</param>
        /// <param name="y">int y</param>
        /// <param name="color">byte[] for the rgb values color</param>
        /// <param name="colorIndex">Index offset in the array</param>
        public abstract void GetColorAt(int x, int y, byte[] color, int colorIndex = 0);

	    /// <summary>
	    ///     Retrieve the color at x,y into the byte*
	    /// </summary>
	    /// <param name="x">int x</param>
	    /// <param name="y">int y</param>
	    /// <param name="color">byte* for the rgb values color</param>
	    /// <param name="colorIndex">Index offset in the pointer</param>
	    public abstract void GetColorAt(int x, int y, byte* color, int colorIndex = 0);

        /// <summary>
        ///     Sets the color at x,y as byte[]
        /// </summary>
        /// <param name="x">int x</param>
        /// <param name="y">int y</param>
        /// <param name="color">byte[] for the rgb values color</param>
        /// <param name="colorIndex">Index offset in the array</param>
		public abstract void SetColorAt(int x, int y, byte[] color, int colorIndex = 0);

	    /// <summary>
	    ///     Sets the color at x,y as byte*
	    /// </summary>
	    /// <param name="x">int x</param>
	    /// <param name="y">int y</param>
	    /// <param name="color">byte* for the rgb values color</param>
	    /// <param name="colorIndex">Index offset in the array</param>
	    public abstract void SetColorAt(int x, int y, byte* color, int colorIndex = 0);

        /// <summary>
        ///     Return the left of the fastbitmap, this is also used as an offset
        /// </summary>
        int IFastBitmapWithOffset.Left
		{
			get { return _left; }
			set { _left = value; }
		}

		/// <summary>
		///     Return the top of the fastbitmap, this is also used as an offset
		/// </summary>
		int IFastBitmapWithOffset.Top
		{
			get { return _top; }
			set { _top = value; }
		}

		/// <summary>
		///     Destructor
		/// </summary>
		~FastBitmapBase()
		{
			Dispose(false);
		}

		// The bulk of the clean-up code is implemented in Dispose(bool)

		/// <summary>
		///     This Dispose is called from the Dispose and the Destructor.
		///     When disposing==true all non-managed resources should be freed too!
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			Unlock();
			if (disposing)
			{
				if (Bitmap != null && NeedsDispose)
				{
					Bitmap.Dispose();
				}
			}
			Bitmap = null;
			BmData = null;
			Pointer = null;
		}

		#region IFastBitmapWithClip

		/// <summary>
		///     Test if the bitmap containt the specified coordinates
		/// </summary>
		/// <param name="x">int x</param>
		/// <param name="y">int y</param>
		/// <returns>true if the specified coordinates are within</returns>
		bool IFastBitmapWithClip.Contains(int x, int y)
		{
			var contains = Clip.Contains(x, y);
			if (InvertClip)
			{
				return !contains;
			}
			return contains;
		}

		/// <inheritdoc />
		void IFastBitmapWithClip.GetColorAt(int x, int y, byte[] color, int colorIndex)
		{
			var contains = Clip.Contains(x, y);
			if (InvertClip && contains)
			{
				// TODO: Implement nearest
				return;
			}
			if (!InvertClip && !contains)
			{
				if (y < Clip.Top)
				{
					y = Clip.Top;
				}
				if (y >= Clip.Bottom)
				{
					y = Clip.Bottom-1;
				}
				if (x < Clip.Left)
				{
					x = Clip.Left;
				}
				if (x >= Clip.Right)
				{
					x = Clip.Right-1;
				}
			}
			GetColorAt(x, y, color, colorIndex);
		}

		/// <summary>
		///     Get the color at the specified location, if it's not clipped
		/// </summary>
		/// <param name="x">int x</param>
		/// <param name="y">int y</param>
		/// <returns>Color</returns>
		Color IFastBitmapWithClip.GetColorAt(int x, int y)
		{
			var contains = Clip.Contains(x, y);
			if (InvertClip && contains)
			{
			    // TODO: Implement nearest
			    return HasAlphaChannel ? Color.Transparent : Color.Black;
			}
			if (!InvertClip && !contains)
			{
				if (y < Clip.Top)
				{
					y = Clip.Top;
				}
				if (y >= Clip.Bottom)
				{
					y = Clip.Bottom-1;
				}
				if (x < Clip.Left)
				{
					x = Clip.Left;
				}
				if (x >= Clip.Right)
				{
					x = Clip.Right-1;
				}
			}
			return GetColorAt(x, y);
		}


		/// <inheritdoc />
		void IFastBitmapWithClip.SetColorAt(int x, int y, byte[] color, int colorIndex)
		{
			var contains = Clip.Contains(x, y);
			if (InvertClip && contains || !InvertClip && !contains)
			{
				return;
			}
			SetColorAt(x, y, color, colorIndex);
		}

		/// <summary>
		///     Set the color at the specified location, if it's not clipped
		/// </summary>
		/// <param name="x">int x</param>
		/// <param name="y">int y</param>
		/// <param name="color">byte array with the color information</param>
		void IFastBitmapWithClip.SetColorAt(int x, int y, ref Color color)
		{
			var contains = Clip.Contains(x, y);
			if (InvertClip && contains || !InvertClip && !contains)
			{
				return;
			}
			SetColorAt(x, y, ref color);
		}

		#endregion

		#region IFastBitmapWithOffset

		/// <summary>
		///     returns true if x & y are inside the FastBitmap
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns>true if x & y are inside the FastBitmap</returns>
		bool IFastBitmapWithOffset.Contains(int x, int y)
		{
			return Area.Contains(x - Left, y - Top);
		}

		/// <summary>
		///     Get the color at the specified location
		/// </summary>
		/// <param name="x">int x</param>
		/// <param name="y">int y</param>
		/// <returns>Color</returns>
		Color IFastBitmapWithOffset.GetColorAt(int x, int y)
		{
			x -= _left;
			y -= _top;
			return GetColorAt(x, y);
		}

	    /// <inheritdoc />
        void IFastBitmapWithOffset.GetColorAt(int x, int y, byte[] color, int colorIndex)
		{
			x -= _left;
			y -= _top;
			GetColorAt(x, y, color, colorIndex);
		}


	    /// <inheritdoc />
	    void IFastBitmapWithOffset.GetColorAt(int x, int y, byte* color, int colorIndex)
	    {
	        x -= _left;
	        y -= _top;
	        GetColorAt(x, y, color, colorIndex);
	    }

        /// <inheritdoc />
        void IFastBitmapWithOffset.SetColorAt(int x, int y, byte* color, int colorIndex)
		{
			x -= _left;
			y -= _top;
			SetColorAt(x, y, color, colorIndex);
		}

	    /// <inheritdoc />
        void IFastBitmapWithOffset.SetColorAt(int x, int y, ref Color color)
		{
			x -= _left;
			y -= _top;
			SetColorAt(x, y, ref color);
		}

		#endregion
	}
}