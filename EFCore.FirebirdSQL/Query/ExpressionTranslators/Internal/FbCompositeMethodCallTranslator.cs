/*                 
 *                    EntityFrameworkCore.FirebirdSQL
*
 *     Permission to use, copy, modify, and distribute this software and its
 *     documentation for any purpose, without fee, and without a written
 *     agreement is hereby granted, provided that the above copyright notice
 *     and this paragraph and the following two paragraphs appear in all copies. 
 * 
 *     The contents of this file are subject to the Initial
 *     Developer's Public License Version 1.0 (the "License");
 *     you may not use this file except in compliance with the
 *     License.
*
 *
 *     Software distributed under the License is distributed on
 *     an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either
 *     express or implied.  See the License for the specific
 *     language governing rights and limitations under the License.
 *
 *      Credits: Rafael Almeida (ralms@ralms.net)
 *                              Sergipe-Brazil
 *                  All Rights Reserved.
 */



namespace Microsoft.EntityFrameworkCore.Query.ExpressionTranslators.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class FbCompositeMethodCallTranslator : RelationalCompositeMethodCallTranslator
    {
	    private static readonly IMethodCallTranslator[] _methodCallTranslators =
	    {
		    new FbContainsOptimizedTranslator(),
		    new FbConvertTranslator(),
		    new FbDateAddTranslator(),
		    new FbEndsWithOptimizedTranslator(),
		    new FbMathTranslator(),
		    new FbNewGuidTranslator(),
		    new FbObjectToStringTranslator(),
		    new FbRegexIsMatchTranslator(),
		    new FbStartsWithOptimizedTranslator(),
		    new FbStringIsNullOrWhiteSpaceTranslator(),
		    new FbStringReplaceTranslator(),
		    new FbStringSubstringTranslator(),
		    new FbStringToLowerTranslator(),
		    new FbStringToUpperTranslator(),
		    new FbStringTrimTranslator()
	    };

	    /// <summary>
	    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
	    ///     directly from your code. This API may change or be removed in future releases.
	    /// </summary>
	    public FbCompositeMethodCallTranslator(
		    RelationalCompositeMethodCallTranslatorDependencies dependencies)
		    : base(dependencies)
	    {
		    // ReSharper disable once DoNotCallOverridableMethodsInConstructor
		    AddTranslators(_methodCallTranslators);
	    }
    }
}
