# Cryptographic agility in .NET and Libraries

## Introduction

.NET should strive to include **cryptographic agility** in our products. That is, where feasible, we should avoid hardcoding our products to use specific cryptographic algorithms. Instead, crypto agility encourages an extensible architecture, where a protocol's cryptographic algorithms can be swapped out naturally and noninvasively.

_All_ cryptographic algorithms have a lifetime beyond which they should no longer be used. Cryptographic agility principles state that protocol designers should be aware of this and should not permanently lock the protocol to any specific cryptographic algorithm, as this could make the protocol unusable when the algorithm eventually expires. 

Crypto agility also helps our compliance story. Some industries (banking, health care, government) have very specific requirements on which cryptographic algorithms they must use or must forbid. By creating crypto-agile products and providing the necessary extensibility points, we help our customers use our products across a wider space.

See also the following references:
* [Wikipedia's entry on crypto agility](https://en.wikipedia.org/wiki/Cryptographic_agility)
* [Bryan Sullivan's 2008 brief on crypto agility](https://aka.ms/CryptographicAgility)
* [Microsoft SDL requirements on crypto agility](https://liquid.microsoft.com/Web/Object/Read/ms.security/Requirements/Microsoft.Security.Cryptography.10023) (Microsoft employees only)

## What kind of code does this apply to?

If you write code that meets _both_ of the below criteria, you should consider whether cryptographic agility is appropriate for your scenario.

1. You consume algorithms from the _System.Security.Cryptography_ namespace. This includes `AES`, `SHA*`, `RSA`, `PBKDF2`, and other algorithmic classes.

2. The input into / output from these algorithms crosses a process boundary. That is, this data is persisted to disk, or it's transmitted across the network, or it meets some other criteria for being considered part of a data format or protocol.

## What is an example of a crypto-agile protocol?

**[JWT](https://jwt.io/)** is an example of a crypto-agile protocol. The payload format contains a header which specifies the algorithm used to protect the remainder of the payload. The decoded header might read:

```json
{
  "alg": "HS256",
  "typ": "JWT"
}
```

The verifier knows that `"alg": "HS256"` maps to HMAC-SHA-256, and it can perform signature validation before trusting the remainder of the payload. The header could instead contain `"alg": "RS*"`, which a compatible verifier would know maps to the RSA family of algorithms. This mechanism allows new algorithms to be substituted in with simple configuration changes to both the generator and the verifier; no change to the JWT spec itself is necessary.

> ⚠️ **Caution**
>
> Naïve implementation of crypto agility could end up making an application _less_ secure. Some misconfigured JWT verifiers accept `"alg": "none"`, and [attackers sometimes abuse this](https://auth0.com/blog/critical-vulnerabilities-in-json-web-token-libraries/) to spoof authentication tokens. Seek security review if you have questions about how crypto agility impacts your component's security stance.

**[ASP.NET Core Data Protection](https://learn.microsoft.com/aspnet/core/security/data-protection/implementation/)** is another example of a crypto-agile protocol. All algorithmic information and parameters are stored in a configuration file and shared across the web cluster. While Data Protection does offer opinionated defaults for algorithm selection, this is easily overridable by the developer, and the protocol is designed to allow defaults to change without invalidating existing payloads.

## What is an example of a non-agile protocol?

**Git** is an example of a protocol that is not crypto-agile. It and its ecosystem are hardcoded to use SHA-1 hashes as identifiers. There are ongoing efforts to make it crypto-agile and to get it to properly support SHA-256 and future hash algorithms. But these efforts are invasive to the protocol (and thus the Git ecosystem at large), so progress has been slow-going. 

## Other guidelines

When you analyze your code to see if it meets the requirements of agility, consider the above examples, keeping an eye toward how invasive it would be for you to support a new algorithm. If it can be accomplished today via plugin / config, you're probably already crypto-agile. If it would require a minimal code change (like adding a case inside a switch statement), you're probably ok as a weaker form of agility, but you should review your user story to verify that you don't need a stronger form of agility. If it would be a highly invasive code change, you're probably not crypto-agile, and you should review whether you need to be agile or whether you require an exemption.

Cryptographic agility _does not_ require you to support every possible algorithm out of the box. (And often you don't want this; see the earlier JWT example!) Consider a payload that contains some embedded parameter `(alg: "Anteater")`. It's perfectly fine for the receiver to say any of these things:

* Anteater? Never heard of it. I'm going to fail because nobody has told me how to interpret this payload.
* Anteater? I know what that means, but I'm operating in a mode where the app has to explicitly opt in to what algorithms I support, and they haven't opted in to that. So I'm going to fail.
* Anteater? I know what that means, but I also know that it is long dead. So I'm going to fail since I no longer trust it.

Of course, if you _did_ know what the Anteater algorithm was and you wanted to support it, feel free! The list above mostly demonstrates that you're not _obligated_ to support everything under the sun. Some protocols or higher-level building blocks (especially those originating at NIST) may even place restrictions on what primitives are legal for use within the protocol.

## Primitive algorithm requirements

> The links and contact information in this section are only accessible to Microsoft employees.

As a reminder, if you're using low-level primitives, Microsoft cryptographic standards presently (as of March 2023) require the use of one of these as a default selection:

* **Symmetric block cipher:** AES-CBC, AES-CTS, AES-XTS ([SDL requirements](https://liquid.microsoft.com/Web/Object/Read/ms.security/Requirements/Microsoft.Security.Cryptography.10002))
* **Unkeyed digest:** SHA2-256, SHA2-384, or SHA2-512 ([SDL requirements](https://liquid.microsoft.com/Web/Object/Read/ms.security/Requirements/Microsoft.Security.Cryptography.10021))
* **Keyed digest:** HMAC-SHA2-256, HMAC-SHA2-384, or HMAC-SHA2-512 ([SDL requirements](https://liquid.microsoft.com/Web/Object/Read/ms.security/Requirements/Microsoft.Security.Cryptography.10020))
* **Authenticated encryption:** _(security review required)_ ([SDL requirements](https://liquid.microsoft.com/Web/Object/Read/ms.security/Requirements/Microsoft.Security.Cryptography.10011))
* **Asymmetric algorithms:** _(security review required)_ ([SDL requirements](https://liquid.microsoft.com/Web/Object/Read/ms.security/Requirements/Microsoft.Security.Cryptography.10012))

Your security advisor can also help with issues regarding algorithmic selection, key generation and persistence, and lifetime management and rolling. For more advanced issues, your security advisor can facilitate conversations with the Crypto Board.

If there are any questions or you need further guidance, please feel free to contact [**fxsecurity**](mailto:fxsecurity@microsoft.com).
