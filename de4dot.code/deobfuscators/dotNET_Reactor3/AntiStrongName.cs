﻿/*
    Copyright (C) 2011 de4dot@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using de4dot.blocks;

namespace de4dot.code.deobfuscators.dotNET_Reactor3 {
	class AntiStrongName {
		public bool remove(Blocks blocks) {
			var allBlocks = blocks.MethodBlocks.getAllBlocks();
			foreach (var block in allBlocks) {
				if (remove(blocks, block))
					return true;
			}

			return false;
		}

		bool remove(Blocks blocks, Block block) {
			var instrs = block.Instructions;
			const int numInstrsToRemove = 11;
			if (instrs.Count < numInstrsToRemove)
				return false;
			int startIndex = instrs.Count - numInstrsToRemove;
			int index = startIndex;

			if (instrs[index++].OpCode.Code != Code.Ldtoken)
				return false;
			if (!checkCall(instrs[index++], "System.Type System.Type::GetTypeFromHandle(System.RuntimeTypeHandle)"))
				return false;
			if (!checkCall(instrs[index++], "System.Reflection.Assembly System.Type::get_Assembly()"))
				return false;
			if (!checkCall(instrs[index++], "System.Reflection.AssemblyName System.Reflection.Assembly::GetName()"))
				return false;
			if (!checkCall(instrs[index++], "System.Byte[] System.Reflection.AssemblyName::GetPublicKeyToken()"))
				return false;
			if (!checkCall(instrs[index++], "System.String System.Convert::ToBase64String(System.Byte[])"))
				return false;
			if (instrs[index++].OpCode.Code != Code.Ldstr)
				return false;
			if (!checkCall(instrs[index++], "System.String", "(System.String,System.String)"))
				return false;
			if (instrs[index++].OpCode.Code != Code.Ldstr)
				return false;
			if (!checkCall(instrs[index++], "System.Boolean System.String::op_Inequality(System.String,System.String)"))
				return false;
			if (!instrs[index++].isBrfalse())
				return false;

			var badBlock = block.FallThrough;
			if (badBlock == null)
				return false;
			if (badBlock.Sources.Count != 1)
				return false;
			var goodblock = block.Targets[0];

			instrs = badBlock.Instructions;
			if (instrs.Count != 12)
				return false;
			index = 0;
			if (!instrs[index++].isLdcI4())
				return false;
			if (instrs[index].OpCode.Code != Code.Stloc_S)
				return false;
			var local = instrs[index++].Operand as VariableDefinition;
			if (local == null)
				return false;
			if (!checkLdloc(blocks.Locals, instrs[index++], local))
				return false;
			if (!checkLdloc(blocks.Locals, instrs[index++], local))
				return false;
			if (instrs[index++].OpCode.Code != Code.Sub)
				return false;
			if (instrs[index++].OpCode.Code != Code.Conv_U1)
				return false;
			if (!checkStloc(blocks.Locals, instrs[index++], local))
				return false;
			if (!checkLdloc(blocks.Locals, instrs[index++], local))
				return false;
			if (!checkLdloc(blocks.Locals, instrs[index++], local))
				return false;
			if (instrs[index++].OpCode.Code != Code.Div)
				return false;
			if (instrs[index++].OpCode.Code != Code.Conv_U1)
				return false;
			if (!checkStloc(blocks.Locals, instrs[index++], local))
				return false;

			block.replaceLastInstrsWithBranch(numInstrsToRemove, goodblock);
			badBlock.Parent.removeDeadBlock(badBlock);

			return true;
		}

		static bool checkCall(Instr instr, string methodFullname) {
			if (instr.OpCode.Code != Code.Call && instr.OpCode.Code != Code.Callvirt)
				return false;
			var calledMethod = instr.Operand as MethodReference;
			if (calledMethod == null)
				return false;
			return calledMethod.ToString() == methodFullname;
		}

		static bool checkCall(Instr instr, string returnType, string parameters) {
			if (instr.OpCode.Code != Code.Call && instr.OpCode.Code != Code.Callvirt)
				return false;
			var calledMethod = instr.Operand as MethodReference;
			if (calledMethod == null)
				return false;
			return DotNetUtils.isMethod(calledMethod, returnType, parameters);
		}

		static bool checkLdloc(IList<VariableDefinition> locals, Instr instr, VariableDefinition local) {
			if (!instr.isLdloc())
				return false;
			if (Instr.getLocalVar(locals, instr) != local)
				return false;
			return true;
		}

		static bool checkStloc(IList<VariableDefinition> locals, Instr instr, VariableDefinition local) {
			if (!instr.isStloc())
				return false;
			if (Instr.getLocalVar(locals, instr) != local)
				return false;
			return true;
		}
	}
}