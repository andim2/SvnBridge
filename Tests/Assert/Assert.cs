using System;
using System.Collections;
using System.Collections.Generic;

namespace CodePlex.NUnitExtensions
{
    public static class Assert
    {
        #region Delegates

        public delegate void ExceptionDelegate();

        #endregion

        public new static bool Equals(object a,
                                      object b)
        {
            throw new InvalidOperationException("Assert.Equals should not be used");
        }

        public new static bool ReferenceEquals(object a,
                                               object b)
        {
            throw new InvalidOperationException("Assert.ReferenceEquals should not be used");
        }

        public static void Equal<T>(T expected,
                                    T actual)
        {
            Equal(expected, actual, (string) null);
        }

        public static void Equal<T>(T expected,
                                    T actual,
                                    string userMessage)
        {
            Equal(expected, actual, GetComparer<T>(), userMessage);
        }

        public static void Equal<T>(T expected,
                                    T actual,
                                    IComparer<T> comparer)
        {
            Equal(expected, actual, comparer, null);
        }

        public static void Equal<T>(T expected,
                                    T actual,
                                    IComparer<T> comparer,
                                    string userMessage)
        {
            if (comparer.Compare(actual, expected) == 0)
            {
                return;
            }

            throw new EqualException(expected, actual, userMessage);
        }

        public static void NotEqual<T>(T expected,
                                       T actual)
        {
            NotEqual(expected, actual, (string) null);
        }

        public static void NotEqual<T>(T expected,
                                       T actual,
                                       string userMessage)
        {
            NotEqual(actual, expected, GetComparer<T>(), userMessage);
        }

        public static void NotEqual<T>(T expected,
                                       T actual,
                                       IComparer<T> comparer)
        {
            NotEqual(actual, expected, comparer, null);
        }

        public static void NotEqual<T>(T actual,
                                       T expected,
                                       IComparer<T> comparer,
                                       string userMessage)
        {
            if (comparer.Compare(actual, expected) != 0)
            {
                return;
            }

            throw new NotEqualException(userMessage);
        }

        public static void True(bool condition)
        {
            True(condition, null);
        }

        public static void True(bool condition,
                                string userMessage)
        {
            if (condition)
            {
                return;
            }

            throw new TrueException(userMessage);
        }

        public static void False(bool condition)
        {
            False(condition, null);
        }

        public static void False(bool condition,
                                 string userMessage)
        {
            if (!condition)
            {
                return;
            }

            throw new FalseException(userMessage);
        }

        public static void Null(object @object)
        {
            Null(@object, null);
        }

        public static void Null(object @object,
                                string userMessage)
        {
            if (@object == null)
            {
                return;
            }

            throw new NullException(@object, userMessage);
        }

        public static void NotNull(object @object)
        {
            NotNull(@object, null);
        }

        public static void NotNull(object @object,
                                   string userMessage)
        {
            if (@object != null)
            {
                return;
            }

            throw new NotNullException(userMessage);
        }

        public static T IsType<T>(object @object)
        {
            return (T) IsType(typeof (T), @object);
        }

        public static object IsType(Type expectedType,
                                    object @object)
        {
            return IsType(expectedType, @object, null);
        }

        public static T IsType<T>(object @object,
                                  string userMessage)
        {
            return (T) IsType(typeof (T), @object, userMessage);
        }

        public static object IsType(Type expectedType,
                                    object @object,
                                    string userMessage)
        {
            if (expectedType.Equals(@object.GetType()))
            {
                return @object;
            }

            throw new IsTypeException(expectedType, @object, userMessage);
        }

        public static void InRange<T>(T actualValue,
                                      T low,
                                      T high)
        {
            InRange(actualValue, low, high, null);
        }

        public static void InRange<T>(T actualValue,
                                      T low,
                                      T high,
                                      string userMessage)
        {
            IComparer<T> comparer = GetComparer<T>();
            InRange(actualValue, low, high, comparer, userMessage);
        }

        public static void InRange<T>(T actualValue,
                                      T low,
                                      T high,
                                      IComparer<T> comparer,
                                      string userMessage)
        {
            if (comparer.Compare(low, actualValue) <= 0 && comparer.Compare(actualValue, high) <= 0)
            {
                return;
            }

            throw new InRangeException(actualValue, low, high, userMessage);
        }

        public static void Same(object expected,
                                object actual)
        {
            Same(expected, actual, null);
        }

        public static void Same(object expected,
                                object actual,
                                string userMessage)
        {
            if (object.ReferenceEquals(actual, expected))
            {
                return;
            }

            throw new SameException(expected, actual, userMessage);
        }

        public static void NotSame(object expected,
                                   object actual)
        {
            NotSame(expected, actual, null);
        }

        public static void NotSame(object expected,
                                   object actual,
                                   string userMessage)
        {
            if (!object.ReferenceEquals(actual, expected))
            {
                return;
            }

            throw new NotSameException(userMessage);
        }

        public static void Contains<T>(T expected,
                                       IEnumerable<T> collection)
        {
            Contains(expected, collection, GetComparer<T>());
        }

        public static void Contains<T>(T expected,
                                       IEnumerable<T> collection,
                                       IComparer<T> comparer)
        {
            foreach (T item in collection)
            {
                if (comparer.Compare(expected, item) == 0)
                {
                    return;
                }
            }

            throw new ContainsException(expected);
        }

        public static void DoesNotContain<T>(T expected,
                                             IEnumerable<T> collection)
        {
            DoesNotContain(expected, collection, GetComparer<T>());
        }

        public static void DoesNotContain<T>(T expected,
                                             IEnumerable<T> collection,
                                             IComparer<T> comparer)
        {
            foreach (T item in collection)
            {
                if (comparer.Compare(expected, item) == 0)
                {
                    throw new DoesNotContainException(expected);
                }
            }
        }

        public static void Empty(IEnumerable collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException("cannot be null");
            }

            foreach (object @object in collection)
            {
                throw new EmptyException();
            }
        }

        public static void NotEmpty<T>(ICollection<T> collection)
        {
            if (collection.Count > 0)
            {
                return;
            }

            throw new NotEmptyException();
        }

        public static T Throws<T>(ExceptionDelegate testCode)
            where T : Exception
        {
            Exception exception = null;

            try
            {
                testCode();
            }
            catch (Exception thrownException)
            {
                exception = thrownException;
            }

            if (exception == null)
            {
                throw new ThrowsException(typeof (T));
            }

            if (!typeof (T).Equals(exception.GetType()))
            {
                throw new ThrowsException(exception, typeof (T));
            }

            return (T) exception;
        }

        public static void DoesNotThrow(ExceptionDelegate testCode)
        {
            testCode();
        }

        private static IComparer<T> GetComparer<T>()
        {
            return new AssertComparer<T>();
        }

        #region Nested type: AssertComparer

        private class AssertComparer<T> : IComparer<T>
        {
            #region IComparer<T> Members

            public int Compare(T x,
                               T y)
            {
                if (x == null)
                {
                    if (y == null)
                    {
                        return 0;
                    }
                    return -1;
                }

                if (y == null)
                {
                    return -1;
                }

                if (x.GetType() != y.GetType())
                {
                    return -1;
                }

                if (x.GetType().IsArray)
                {
                    Array xArray = x as Array;
                    Array yArray = y as Array;

                    if (xArray != null && yArray != null)
                    {
                        if (xArray.Rank != 1)
                        {
                            throw new ArgumentException("Multi-dimension array comparison is not supported");
                        }

                        if (xArray.Length != yArray.Length)
                        {
                            return -1;
                        }

                        for (int index = 0; index < xArray.Length; index++)
                        {
                            if (!Equals(xArray.GetValue(index), yArray.GetValue(index)))
                            {
                                return -1;
                            }
                        }
                    }

                    return 0;
                }

                IComparable<T> comparable1 = x as IComparable<T>;

                if (comparable1 != null)
                {
                    return comparable1.CompareTo(y);
                }

                IComparable comparable2 = x as IComparable;

                if (comparable2 != null)
                {
                    return comparable2.CompareTo(y);
                }

                return Equals(x, y) ? 0 : -1;
            }

            #endregion
        }

        #endregion
    }
}