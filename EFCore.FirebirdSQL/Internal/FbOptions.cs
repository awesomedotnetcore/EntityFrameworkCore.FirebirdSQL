/*                 
 *                    EntityFrameworkCore.FirebirdSQL
 *     
*
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
 *
 *
 *                              
 *                  All Rights Reserved.
 */

using System; 
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal; 
using EntityFrameworkCore.FirebirdSQL.Utilities;

namespace Microsoft.EntityFrameworkCore.Internal
{
    public class FbOptions : IFbOptions
    {
	    private Lazy<FbSettings> _fbSettings;
	    public virtual FbSettings Settings => _fbSettings.Value; 

		public virtual void Initialize(IDbContextOptions options)
        {
			var fbOptions = GetOptions(options); 
	        _fbSettings = new Lazy<FbSettings>(() => fbOptions.Connection != null
		                                                 ? new FbSettings().GetSettings(fbOptions.Connection)
		                                                 : new FbSettings().GetSettings(fbOptions.ConnectionString));
		}

        public virtual void Validate(IDbContextOptions options)
        {
			var fbOptions = GetOptions(options);
		}

	    private FbOptionsExtension GetOptions(IDbContextOptions options)
		    => options.FindExtension<FbOptionsExtension>() ?? new FbOptionsExtension();

    }
}