﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenRasta.Pipeline;
using OpenRasta.Web;
using Shouldly;
using Tests.Pipeline.Middleware.Infrastructrure;
using Xunit;

namespace Tests.Pipeline.Middleware.Interception
{
  public class intercepted_middleware
  {
    [Fact]
    public void middleware_is_intercepted()
    {
      var calls = new[]
      {
        new ContributorCall(new DoNothingContributor(), OpenRasta.Pipeline.Middleware.IdentitySingleTap, "doNothing")
      };
      var middlewareChain = calls.ToMiddleware(new Dictionary<Func<ContributorCall, bool>, Func<IPipelineMiddlewareFactory>>
      {
        [call=>call.Target is DoNothingContributor] = () => new WrapperMiddleware()
      }).ToArray();
      middlewareChain[0].ShouldBeOfType<PreExecuteMiddleware>();
      middlewareChain[1].ShouldBeOfType<WrapperMiddleware>();
    }
  }

  public class WrapperMiddleware : IPipelineMiddlewareFactory
  {

    public WrapperMiddleware()
    {
    }

    public IPipelineMiddleware Compose(IPipelineMiddleware next)
    {
      return new InBetweenMiddleware(next);
    }
  }

  public class InBetweenMiddleware : IPipelineMiddleware
  {
    readonly IPipelineMiddleware _next;

    public InBetweenMiddleware(IPipelineMiddleware next)
    {
      _next = next;
    }

    public Task Invoke(ICommunicationContext env)
    {
      return _next.Invoke(env);
    }
  }
}