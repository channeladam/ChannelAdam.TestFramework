//-----------------------------------------------------------------------
// <copyright file="TestEasy.cs">
//     Copyright (c) 2014-2020 Adam Craven. All rights reserved.
// </copyright>
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//-----------------------------------------------------------------------

namespace ChannelAdam.TestFramework.Abstractions
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading.Tasks;

    using ChannelAdam.Logging;
    using ChannelAdam.Logging.Abstractions;

    /// <summary>
    /// Abstract class that provides helpful functionality that can be used to more easily implement tests.
    /// </summary>
    public abstract class TestEasy
    {
        private readonly ISimpleLogger logger;
        private readonly ILogAsserter logAssert;

        private Exception? actualException;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestEasy" /> class.
        /// </summary>
        /// <param name="logAssert">The log asserter.</param>
        protected TestEasy(ILogAsserter logAssert) : this(new SimpleConsoleLogger(), logAssert)
        {
        }

        protected TestEasy(ISimpleLogger logger, ILogAsserter logAssert)
        {
            this.logger = logger;
            this.logAssert = logAssert;
            this.ExpectedException = new ExpectedExceptionDescriptor(logger);
        }

        #region Properties

        /// <summary>
        /// Gets the log asserter.
        /// </summary>
        /// <value>
        /// The log assert.
        /// </value>
        protected virtual ILogAsserter LogAssert
        {
            get
            {
                return this.logAssert;
            }
        }

        /// <summary>
        /// Gets the simple logger.
        /// </summary>
        /// <value>
        /// The logger.
        /// </value>
        protected virtual ISimpleLogger Logger
        {
            get
            {
                return this.logger;
            }
        }

        /// <summary>
        /// Gets the descriptor for the expected exception.
        /// </summary>
        /// <value>
        /// The expected exception descriptor.
        /// </value>
        protected ExpectedExceptionDescriptor ExpectedException { get; }

        /// <summary>
        /// Gets or sets the actual exception that occurred.
        /// </summary>
        /// <value>
        /// The actual exception that occurred.
        /// </value>
        protected Exception? ActualException
        {
            get
            {
                return this.actualException;
            }

            set
            {
                this.actualException = value;

                if (value != null)
                {
                    this.Logger.Log();
                    this.Logger.Log("Setting ActualException: '{0}'", value.Message);
                    this.Logger.Log(">>> START OF EXCEPTION DETAIL >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>");
                    this.Logger.Log(value.ToString());
                    this.Logger.Log("<<< END OF EXCEPTION DETAIL <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<");
                    this.Logger.Log();
                }
            }
        }

        #endregion Properties

        #region Try Methods

        /// <summary>
        /// Performs the given action and catches exceptions into <see cref="ActualException"/>.
        /// </summary>
        /// <param name="action">The action.</param>
        protected void Try(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                this.ActualException = ex;
            }
        }

        /// <summary>
        /// Performs the given asynchronous action that returns a Task, waits for it finish, and catches exceptions into <see cref="ActualException"/>.
        /// </summary>
        /// <param name="action">The asynchronous action.</param>
        protected void Try(Func<Task> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            try
            {
                action.Invoke().Wait();
            }
            catch (Exception ex)
            {
                this.ActualException = ex;
            }
        }

        #endregion

        /// <summary>
        /// Asserts that no actual exception occurred.
        /// </summary>
        protected void AssertNoExceptionOccurred()
        {
            this.LogAssert.IsNull("ActualException", this.ActualException);
        }

        /// <summary>
        /// Asserts that the specified expected exception occurred.
        /// </summary>
        /// <param name="expectedExceptionType">Expected type of the exception.</param>
        /// <remarks>
        /// Asserts that the expected exception as described by the property ExpectedException.
        /// </remarks>
        protected void AssertExpectedException(Type expectedExceptionType)
        {
            this.ExpectedException.ExpectedType = expectedExceptionType;
            this.AssertExpectedException();
        }

        /// <summary>
        /// Asserts that the expected exception occurred.
        /// </summary>
        /// <remarks>
        /// Asserts that the expected exception as described by the property ExpectedException.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "ActualException", Justification = "As desired.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "ChannelAdam.TestFramework.ILogAsserter.Fail(System.String,System.Object[])", Justification = "Not globalizing.")]
        protected void AssertExpectedException()
        {
            if (!this.ExpectedException.IsExpected)
            {
                this.LogAssert.Fail("There is no expected exception defined.");
            }

            if (this.ActualException == null)
            {
                this.LogAssert.Fail("ActualException is null");
                return; // Nullable check does not know that Fail throws an exception.
            }

            string exceptionMessage;

            if (this.ActualException is AggregateException aggregateException)
            {
                exceptionMessage = string.Join(
                    Environment.NewLine + " === INNER EXCEPTION ==>: ",
                    aggregateException.Flatten().InnerExceptions.Select(ex => ex.Message));
            }
            else
            {
                exceptionMessage = this.ActualException.Message;
            }

            Type? expectedExceptionType = this.ExpectedException.ExpectedType;
            string? expectedExceptionMessageShouldContainText = this.ExpectedException.MessageShouldContainText;

            if (expectedExceptionType != null)
            {
                this.LogAssert.IsInstanceOfType("ActualException", expectedExceptionType, this.ActualException);
            }

            if (!string.IsNullOrWhiteSpace(expectedExceptionMessageShouldContainText))
            {
                this.LogAssert.StringContains("ActualException's message text", expectedExceptionMessageShouldContainText, exceptionMessage);
            }
        }

        /// <summary>
        /// Set a private property on an object.
        /// </summary>
        /// <param name="propertyExpression">The expression for the property to set.</param>
        /// <param name="value">The value to set.</param>
        /// <typeparam name="T">The type of the object to set a value on.</typeparam>
        /// <exception cref="ArgumentException">Thrown when the expression is not a property.</exception>
        protected static void SetPrivateProperty<T>(Expression<Func<T>> propertyExpression, object value)
        {
            if (propertyExpression.Body is not MemberExpression memberExpression
                    || (memberExpression?.Member?.MemberType != MemberTypes.Property)
                    || (memberExpression?.Expression is null))
            {
                throw new ArgumentException("Expression needs to be for a property", nameof(propertyExpression));
            }

            object? obj = Expression.Lambda(memberExpression.Expression).Compile().DynamicInvoke();
            if (obj is null)
            {
                throw new ArgumentException("Expression did not evaluate to an object", nameof(propertyExpression));
            }

            var property = (PropertyInfo)memberExpression.Member;
            property.SetValue(obj, value);
        }
    }
}