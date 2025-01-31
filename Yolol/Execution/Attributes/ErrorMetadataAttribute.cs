﻿using System;

namespace Yolol.Execution.Attributes
{
    /// <summary>
    /// Tags an operator overload with methods that check if it might/will throw
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ErrorMetadataAttribute
        : Attribute
    {
        public bool IsInfallible { get; }
        public string? WillThrow { get; }
        public string? UnsafeAlternative { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="willThrow">The name of a method which checks if the operation (given value(s)) will throw.</param>
        /// <param name="unsafeAlternative">An alternative implementation of the tagged method which does not have a runtime check</param>
        public ErrorMetadataAttribute(string willThrow, string? unsafeAlternative = null)
        {
            IsInfallible = false;
            WillThrow = willThrow;
            UnsafeAlternative = unsafeAlternative;
        }
    }
}
