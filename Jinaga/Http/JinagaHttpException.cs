using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Jinaga.Http
{
    /// <summary>
    /// A replicator answered with a status outside the 2xx range. The status and the response
    /// body are carried as properties, so a caller classifies the failure by type and by
    /// <see cref="StatusCode"/> rather than by matching the exception message.
    /// </summary>
    /// <remarks>
    /// This derives from <see cref="HttpRequestException"/> because that is the type this library
    /// threw for every non-success status before the hierarchy existed. Existing
    /// <c>catch (HttpRequestException)</c> clauses therefore keep working.
    /// </remarks>
    public class JinagaHttpException : HttpRequestException
    {
        public JinagaHttpException(HttpStatusCode statusCode, string body)
            : base($"Error {statusCode}: {body}")
        {
            StatusCode = statusCode;
            Body = body;
        }

        /// <summary>
        /// The status the replicator returned. Includes statuses that are not members of
        /// <see cref="HttpStatusCode"/>, such as 419.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// The response body, verbatim. Empty when the body could not be read.
        /// </summary>
        public string Body { get; }

        /// <summary>
        /// Chooses the exception that represents this response. Every non-success status yields
        /// one of these types, so the classification lives here and nowhere else.
        /// </summary>
        /// <param name="requestUri">
        /// The URI of the request that produced the response, used to name the feed of a
        /// <see cref="FeedNotFoundException"/>. May be relative.
        /// </param>
        internal static JinagaHttpException ForResponse(HttpStatusCode statusCode, string body, Uri? requestUri)
        {
            if (statusCode == HttpStatusCode.Forbidden)
            {
                return new ForbiddenException(ReasonFromBody(body), body);
            }

            if (statusCode == HttpStatusCode.NotFound &&
                body.Trim() == FeedNotFoundException.FeedNotFoundBody)
            {
                string? feed = FeedFromRequestUri(requestUri);
                if (feed != null)
                {
                    return new FeedNotFoundException(feed);
                }
            }

            return new JinagaHttpException(statusCode, body);
        }

        /// <summary>
        /// Reads the refusal reason out of a JSON body's <c>reason</c> or <c>message</c>
        /// property. A body that is not such a JSON object is itself the reason.
        /// </summary>
        private static string ReasonFromBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return body;
            }

            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (string property in new[] { "reason", "message" })
                    {
                        if (document.RootElement.TryGetProperty(property, out var value) &&
                            value.ValueKind == JsonValueKind.String)
                        {
                            return value.GetString() ?? body;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // The body is not JSON, so it is the reason as it stands.
            }

            return body;
        }

        /// <summary>
        /// The feed hash in a path of the form <c>.../feeds/{hash}</c>, or null when the request
        /// was not for a feed.
        /// </summary>
        private static string? FeedFromRequestUri(Uri? requestUri)
        {
            if (requestUri == null)
            {
                return null;
            }

            string path = requestUri.IsAbsoluteUri
                ? requestUri.AbsolutePath
                : requestUri.OriginalString.Split('?')[0];
            var segments = path.Split('/').Where(segment => segment.Length > 0).ToList();
            return segments.Count >= 2 && segments[segments.Count - 2] == "feeds"
                ? segments[segments.Count - 1]
                : null;
        }
    }

    /// <summary>
    /// The replicator refused a request on a distribution or authorization rule, answering 403
    /// with the reason in the body.
    /// </summary>
    public class ForbiddenException : JinagaHttpException
    {
        public ForbiddenException(string reason, string body)
            : base(HttpStatusCode.Forbidden, body)
        {
            Reason = reason;
        }

        /// <summary>
        /// Why the request was refused: a JSON body's <c>reason</c> or <c>message</c> property,
        /// else the body verbatim.
        /// </summary>
        public string Reason { get; }
    }

    /// <summary>
    /// A feed registration is gone, because the replicator restarted or reloaded its policy. The
    /// owner of the feed has to register it again.
    /// </summary>
    public class FeedNotFoundException : JinagaHttpException
    {
        /// <summary>
        /// The body with which a replicator reports that a feed is no longer registered. This is
        /// the only body that produces this exception.
        /// </summary>
        public const string FeedNotFoundBody = "feed_not_found";

        public FeedNotFoundException(string feed)
            : base(HttpStatusCode.NotFound, FeedNotFoundBody)
        {
            Feed = feed;
        }

        /// <summary>
        /// The hash of the feed that is no longer registered.
        /// </summary>
        public string Feed { get; }
    }
}
