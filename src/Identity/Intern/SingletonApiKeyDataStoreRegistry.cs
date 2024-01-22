using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tlabs.Data;
using Tlabs.Data.Entity;
using Tlabs.Misc;
using Tlabs.Server.Model;

namespace Tlabs.Identity.Intern {

  ///<summary>ApiKey registry that maintains <see cref="KeyToken"/>(s) in a persistent <see cref="IDataStore"/>.</summary>
  ///<remarks><para>ATTENTION: This must be registers as a singleton with any DI service collection!</para>
  ///<para><see cref="KeyToken"/>(s) are cached in memory.</para>
  ///</remarks>
  public class SingletonApiKeyDataStoreRegistry : IApiKeyRegistry {
    class LazyCache {
      internal static ICache<string, KeyToken> instance;
      static LazyCache() {
        instance= new LookupCache<string, KeyToken>(SingletonApiKeyDataStoreRegistry.internalRregisteredKeys().Select(t => new KeyValuePair<string, KeyToken>(t.TokenName??"", t)));
        log.LogInformation("ApiKey cache initialized with {cnt} key tokens.", instance.Entries.Count());
      }
    }

    static readonly ILogger log= App.Logger<SingletonApiKeyDataStoreRegistry>();
    static ICache<string, KeyToken> cache => LazyCache.instance;
    readonly Options options;

    ///<summary>Ctor from <paramref name="options"/></summary>
    public SingletonApiKeyDataStoreRegistry(IOptions<Options> options) {
      this.options= options.Value;
      if (!cache.Entries.Any() && null != this.options.initialKey) cache[this.options.initialKey]= new KeyToken {
        TokenName= this.options.initialTokenName,
        Description= "Temporary initial Key - Please delete",
        ValidFrom= App.TimeInfo.Now,
        ValidityState= ApiKey.Status.ACTIVE.ToString(),
        ValidUntil= App.TimeInfo.Now.AddHours(this.options.initialValidHours ?? 1),
        Roles= new List<string> { "SUPER_ADMIN" }
      };
    }

    ///<inheritdoc/>
    public KeyToken Register(KeyToken token, string key) {
      if (string.IsNullOrEmpty(key))
        throw new ArgumentNullException(nameof(key));
      if (string.IsNullOrEmpty(token.TokenName))
        throw new ArgumentNullException(nameof(token.TokenName));
      if (null == token.Roles || token.Roles.Count==0)
        throw new ArgumentNullException(nameof(token.Roles));

      ApiKey apiKey= internalRegister(token, key);

      if (null != options.initialKey) cache.Evict(options.initialKey);  //remove initial key if still in cache

      return cache[key]= KeyToken.FromEntity(apiKey); //return cached token
    }

    ///<inheritdoc/>
    public KeyToken? Deregister(string tokenName) {
      var tokenKey= cache.Entries.SingleOrDefault(r => r.Value.TokenName == tokenName).Key;
      if (null != tokenKey) cache.Evict(tokenKey);

      return internalDeregister(tokenName);   //persistently mark as deleted
    }

    ///<inheritdoc/>
    public KeyToken[] RegisteredKeys() => internalRregisteredKeys();

    ///<inheritdoc/>
    public int RegisteredKeyCount() => internalRregisteredKeys().Length;

    ///<inheritdoc/>
    public KeyToken? VerifiedKey(string key) {
      //try to find token in cache and verify
      var token= cache[key];
      if (null != token) {
        if (   token.ValidFrom <= App.TimeInfo.Now
            && (!token.ValidUntil.HasValue || token.ValidUntil > App.TimeInfo.Now)
            && token.ValidityState == ApiKey.Status.ACTIVE.ToString())
          return token;
        //not valid anymore -> remove from cache
        //we don't return here however in case the validity has changed with the data store
        cache.Evict(key);
      }
      //not found -> try to get from data store
      token= internalGetValidKeyToken(key);

      if (null != token) cache[key]= token; //cache if existing

      return token;
    }

    ///<inheritdoc/>
    public string GenerateKey() {
      return generateRandomCryptographicKey(options.genKeyLength ?? 32);
    }

    ///<summary>
    ///Generates a string of the specified length by using the <see cref="RandomNumberGenerator"/>.
    ///The RNG will create a cryptographically strong random sequence of (non-zero) bytes. This will be returned as a B64 string
    ///</summary>
    private static string generateRandomCryptographicKey(int keyLength) {
      var rngGen= RandomNumberGenerator.Create();
      byte[] randomBytes= new byte[keyLength];
      rngGen.GetNonZeroBytes(randomBytes);
      return Convert.ToBase64String(randomBytes);
    }

    static KeyToken[] internalRregisteredKeys() => ReturnFrom((repo, hasher)
      => repo.AllUntracked.LoadRelated(repo.Store, k => k.Roles)!.ThenLoadRelated(repo.Store, k => k.Role)
             .Where(r => r.ValidityState != ApiKey.Status.DELETED.ToString()).Select(r => KeyToken.FromEntity(r)).ToArray()
    ) ?? Array.Empty<KeyToken>();
    static KeyToken? internalGetValidKeyToken(string key) => ReturnFrom((repo, hasher) => {
      var ent= repo.AllUntracked
                   .LoadRelated(repo.Store, x => x.Roles)!.ThenLoadRelated(repo.Store, x => x.Role)
                   .Where(r =>
                     r.ValidFrom <= App.TimeInfo.Now
                     && (r.ValidUntil == null || r.ValidUntil > App.TimeInfo.Now)
                     && r.ValidityState == ApiKey.Status.ACTIVE.ToString()
                   )
                  .AsEnumerable() //Load into memory since VerifyHashedPassword does not evaluate in DB
                  .SingleOrDefault(r => hasher.VerifyHashedPassword(r.TokenName??"", r.Hash??"", key) == PasswordVerificationResult.Success);
      return null != ent ? KeyToken.FromEntity(ent) : null; //?? throw EX.New<InvalidOperationException>("API key not found: {key}", key));
    });

    static ApiKey internalRegister(KeyToken token, string key) => ReturnFrom((repo, hasher) => {
      var hash= hasher.HashPassword(token.TokenName??"", key);

      //first, hash and store in db
      var apiKey= new ApiKey {
        Hash= hash,
        TokenName= token.TokenName,
        Description= token.Description,
        ValidFrom= token.ValidFrom ?? App.TimeInfo.Now,
        ValidUntil= token.ValidUntil,
        ValidityState= ApiKey.Status.ACTIVE.ToString()
      };
      apiKey.Roles= repo.Store.Query<Role>()
                              .Where(r => token.Roles!.Contains(r.Name!))
                              .Select(r => new ApiKey.RoleRef {
                                ApiKey= apiKey,
                                Role= r
                              }).ToList();
      repo.Insert(apiKey);
      repo.Store.CommitChanges();
      return apiKey;
    }) ?? throw EX.New<InvalidOperationException>("Failed to register API key: {key}", token.TokenName ?? "?");

    static KeyToken? internalDeregister(string tokenName) => ReturnFrom((repo, hasher) => {
      var apiKey= repo.All.SingleOrDefault(r => r.TokenName == tokenName);
      if (null == apiKey || apiKey.ValidityState == ApiKey.Status.DELETED.ToString())
        return null;

      apiKey.ValidUntil= App.TimeInfo.Now;
      apiKey.ValidityState= ApiKey.Status.DELETED.ToString();
      apiKey.TokenName+= $"_._{apiKey.ValidityState}@{apiKey.ValidUntil:O}";   //new token name (and hash) to enable key generation of new key with old name / key
      apiKey.Hash= null;
      repo.Store.CommitChanges();
      return KeyToken.FromEntity(apiKey);
    });

    static T? ReturnFrom<T>(Func<IRepo<ApiKey>, IPasswordHasher<string>, T> doWith) {
      T? ret= default;
      Tlabs.App.WithServiceScope(prov => {
        ret= doWith(prov.GetRequiredService<IRepo<ApiKey>>(), prov.GetRequiredService<IPasswordHasher<string>>());
      });
      return ret;
    }

    ///<summary>Options for the registry</summary>
    public class Options {
      ///<summary>initialKey</summary>
      public string? initialKey { get; set; }
      ///<summary>initialTokenName</summary>
      public string? initialTokenName { get; set; }
      ///<summary>initialValidHours</summary>
      public int? initialValidHours { get; set; }
      ///<summary>genKeyLength</summary>
      public int? genKeyLength { get; set; }
    }
  }
}