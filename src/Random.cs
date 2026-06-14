#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Collections.Generic;

namespace CivOne
{
	/// <remarks>
	/// This code is based on JCivED[r23] source code by darkpanda. <http://sourceforge.net/p/jcived/code/HEAD/tree/branches/dev/src/dd/civ/logic/CivRandom.java>
	/// </remarks>
	internal class Random
	{
		private short _initialSeed;
		private long _counter;

		private short _ax, _bx, _cx, _dx;

		private bool _zf, _cf, _of;

		private Stack<short> _stack;

		private short _ds5BDA, _ds5BDC;
		
		private void AssemblyMultiply(short value)
		{
			int val = ((int)value) & 0xFFFF;
			int eax = ((int)_ax) & 0xFFFF;
			eax *= val;
			_dx = (short)(eax >> 16);
			_ax = (short)(eax);
			_cf = (_dx != 0x0);
			_of = _cf;
		}
		private void AssemblyAddAX(short value)
		{
			int eax = (((int)_ax) & 0xFFFF) + (((int)value) & 0xFFFF);
			_cf = ((eax & 0xFFFF0000) != 0);
			_ax = (short)eax;
		}
		private void AssemblyAddDX(short value)
		{
			int edx = (((int)_ax) & 0xFFFF) + (((int)value) & 0xFFFF);
			_cf = ((edx & 0xFFFF0000) != 0);
			_dx = (short)edx;
		}
		private void AssemblyAdcDX(short value)
		{
			int edx = (short)(_dx + value + (_cf ? 1 : 0));
			_cf = ((edx & 0xFFFF0000) != 0);
			_dx = (short)edx;
		}
		private void AssemblyCwd()
		{
			_dx = (short)(_ax < 0 ? -1 : 0);
		}
		private void AssemblyRcrAX(int i)
		{
			bool tempCF = _cf;
			_cf = ((_ax & 0x1) == 1);
			_ax >>= 1;
			if (tempCF) _ax = (short)((ushort)_ax | 0x8000);
			else _ax &= 0x7FFF;
		}
		private void AssemblySarDX()
		{
			_cf = (_dx & 0x1) == 0x1;
			_dx >>= 1;
		}
		
		private void RandomPartFormula(short arg0, short arg2, short arg4, short arg6)
		{
			_ax = arg2;
			_bx = arg6;
			_bx |= _ax;
			_zf = (_bx == 0);
			_bx = arg4;
			if (_zf)
			{
				_ax = arg0;
				AssemblyMultiply(_bx);
				return;
			}
			AssemblyMultiply(_bx);
			_cx = _ax;
			_ax = arg0;
			AssemblyMultiply(arg6);
			_cx += _ax;
			_ax = arg0;
			AssemblyMultiply(_bx);
			AssemblyAddDX(_cx);
		}
		
		private void RandomSub1()
		{
			_ax = 0x43FD;
			_dx = 3;
			RandomPartFormula(DS5BDA, DS5BDC, _ax, _dx);
			AssemblyAddAX((short)(0 & 0x9EC3));
			AssemblyAdcDX(0x26);
			DS5BDA = _ax;
			DS5BDC = _dx;
			_ax = _dx;
			_ax &= 0x7FFF;
		}
		
		private void RandomSub2()
		{
			_cx &= 0xFF;
			while (_cx != 0)
			{
				AssemblySarDX();
				AssemblyRcrAX(1);
				_cx--;
			}
		}
		
		private void DoRandom(short arg0)
		{
			_ax = arg0;
			AssemblyCwd();
			_stack.Push(_dx);
			_stack.Push(_ax);
			
			RandomSub1();
			
			AssemblyCwd();
			_stack.Push(_dx);
			_stack.Push(_ax);
			
			RandomPartFormula(_stack.Pop(), _stack.Pop(), _stack.Pop(), _stack.Pop());
			
			_cx = (short)((_cx & 0xFF00) | 0x0F);
			
			RandomSub2();
		}
		
		public short InitialSeed
		{
			get
			{
				return _initialSeed;
			}
		}
		
		public long Counter
		{
			get
			{
				return _counter;
			}
		}
		
		private short DS5BDA
		{
			get => _ds5BDA;
			set => _ds5BDA = value;
		}

		private short DS5BDC
		{
			get => _ds5BDC;
			set => _ds5BDC = value;
		}

		public override bool Equals(object obj)
		{
			if (obj.GetType() != typeof(Random))
				return false;

			Random tr2 = (Random)obj;
			return _initialSeed == tr2._initialSeed
			    && _counter == tr2._counter
			    && _ds5BDA == tr2._ds5BDA
			    && _ds5BDC == tr2._ds5BDC
			    && _stack.Equals(tr2._stack);
		}

		public override int GetHashCode()
		{
			return _initialSeed;
		}

		public int Next(int max)
		{
			DoRandom((short)Math.Min(max, (int)short.MaxValue));
			_counter++;
			return _ax;
		}

		public int Next(int min, int max)
		{
			DoRandom((short)Math.Min(max - min, (int)short.MaxValue));
			_counter++;
			return _ax + min;
		}
		
		public Random(int seed = -1)
		{
			if (seed == -1)
				seed = (int)DateTime.Now.Ticks;
			DS5BDA = (short)seed;
			DS5BDC = (short)((seed & 0xFFFF0000) >> 16);
			_initialSeed = (short)seed;
			_stack = new Stack<short>();
			_counter = 0;
		}
	}
}