﻿using System.Collections.Generic;
using System.Linq;

namespace ResultZ
{
    public static partial class ResultExtensions
    {
        public static IResult WithSuccess(this IResult result, params Success[] successes) => result.WithSuccess(successes.AsEnumerable());
        public static IResult WithSuccess(this IResult result, IEnumerable<Success> successes) => result.WithReason(successes);
        public static IResult WithSuccess<TSuccess>(this IResult result)
            where TSuccess : Success, new()
            => result.WithSuccess(new TSuccess());

        public static IResult<TValue> WithSuccess<TValue>(this IResult<TValue> result, params Success[] successes) => result.WithSuccess(successes.AsEnumerable());
        public static IResult<TValue> WithSuccess<TValue>(this IResult<TValue> result, IEnumerable<Success> successes) => result.WithReason(successes);
        public static IResult<TValue> WithSuccess<TSuccess, TValue>(this IResult<TValue> result)
            where TSuccess : Success, new()
            => result.WithSuccess(new TSuccess());
    }
}
