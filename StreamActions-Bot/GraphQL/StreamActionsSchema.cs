/*
 * Copyright © 2019-2020 StreamActions Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *  http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using GraphQL.Conversion;
using GraphQL.Types;
using System;
using System.Collections.Generic;

namespace StreamActions.GraphQL
{
    public class StreamActionsSchema : ISchema
    {
        #region Public Properties

        public IEnumerable<Type> AdditionalTypes => throw new NotImplementedException();
        public IEnumerable<IGraphType> AllTypes => throw new NotImplementedException();
        public IEnumerable<DirectiveGraphType> Directives { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IFieldNameConverter FieldNameConverter { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool Initialized => throw new NotImplementedException();
        public IObjectGraphType Mutation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IObjectGraphType Query { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IObjectGraphType Subscription { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        #endregion Public Properties

        #region Public Methods

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public DirectiveGraphType FindDirective(string name) => throw new NotImplementedException();

        public IGraphType FindType(string name) => throw new NotImplementedException();

        public IAstFromValueConverter FindValueConverter(object value, IGraphType type) => throw new NotImplementedException();

        public void Initialize() => throw new NotImplementedException();

        public void RegisterDirective(DirectiveGraphType directive) => throw new NotImplementedException();

        public void RegisterDirectives(params DirectiveGraphType[] directives) => throw new NotImplementedException();

        public void RegisterType(IGraphType type) => throw new NotImplementedException();

        public void RegisterType<T>() where T : IGraphType => throw new NotImplementedException();

        public void RegisterTypes(params IGraphType[] types) => throw new NotImplementedException();

        public void RegisterTypes(params Type[] types) => throw new NotImplementedException();

        public void RegisterValueConverter(IAstFromValueConverter converter) => throw new NotImplementedException();

        #endregion Public Methods

        #region Protected Methods

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        #endregion Protected Methods
    }
}