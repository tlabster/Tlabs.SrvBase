using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Tlabs.Data;
using Tlabs.Data.Entity;
using Tlabs.Misc;
using Tlabs.Server.Model;

namespace Tlabs.Server.Auth {
  ///<inheritdoc/>
  public class DefaultApiKeyRegistry : IApiKeyRegistry {
    private static BasicCache<string, KeyToken> cache= new BasicCache<string, KeyToken>();
    private readonly Options options;

    ///<summary>Ctor</summary>
    public DefaultApiKeyRegistry(IOptions<Options> options) {
      this.options= options.Value;
      if (this.RegisteredKeyCount() == 0)
        cache[this.options.initialKey]= new KeyToken {
          TokenName= this.options.initialTokenName,
          Description= "Initial Key - Please Delete",
          ValidFrom= App.TimeInfo.Now,
          ValidityState= ApiKey.Status.ACTIVE.ToString(),
          ValidUntil= App.TimeInfo.Now.AddHours(this.options.initialValidHours.HasValue ? this.options.initialValidHours.Value : 1)
        };
    }

    ///<inheritdoc/>
    public KeyToken Register(string key, string tokenName, string description = null, DateTime? validUntil = null) {
      if (string.IsNullOrEmpty(key))
        throw new ArgumentNullException("Key cannot be empty");
      if (string.IsNullOrEmpty(tokenName))
        throw new ArgumentNullException("Token name cannot be empty");

      ApiKey apiKey= null;
      App.WithServiceScope(prov => {
        var repo= (IRepo<ApiKey>)prov.GetService(typeof(IRepo<ApiKey>));
        var um= (UserManager<User>)prov.GetService(typeof(UserManager<User>));
        var hash= um.PasswordHasher.HashPassword(null, key);

        //first, hash and store in db
        apiKey= new ApiKey {
          Hash= hash,
          TokenName= tokenName,
          Description= description,
          ValidFrom= App.TimeInfo.Now,
          ValidUntil= validUntil,
          Roles= null, //TODO: roles
          ValidityState= ApiKey.Status.ACTIVE.ToString()
        };

        repo.Insert(apiKey);
        repo.Store.CommitChanges();
      });

      //create token and register with cache
      var keyToken= KeyToken.FromEntity(apiKey);
      cache[key]= keyToken;

      return keyToken;
    }

    ///<inheritdoc/>
    public KeyToken Deregister(string tokenName) {
      KeyToken returnToken= null;
      App.WithServiceScope(prov => {
        var repo= (IRepo<ApiKey>)prov.GetService(typeof(IRepo<ApiKey>));
        var apiKey= repo.AllUntracked.FirstOrDefault(r => r.TokenName == tokenName);

        var token= cache.Entries.FirstOrDefault(r => r.Value.TokenName == tokenName);
        if (token.Key != null)
          cache.Evict(token.Key);
        if (apiKey == null || apiKey.ValidityState == ApiKey.Status.DELETED.ToString())
          return;

        //alter token name and hash to enable key generation of new key with old name / key
        apiKey.TokenName= apiKey.TokenName + Guid.NewGuid().ToString().Substring(0, 8);
        apiKey.Hash= null;
        apiKey.ValidUntil= App.TimeInfo.Now;
        apiKey.ValidityState= ApiKey.Status.DELETED.ToString();
        repo.Update(apiKey);
        repo.Store.CommitChanges();
        returnToken= token.Value ?? KeyToken.FromEntity(apiKey);
      });
      return returnToken;
    }

    ///<inheritdoc/>
    public KeyToken[] RegisteredKeys() {
      KeyToken[] keys= null;
      App.WithServiceScope(prov => {
        var repo= (IRepo<ApiKey>)prov.GetService(typeof(IRepo<ApiKey>));
        keys= repo.AllUntracked.Where(r => r.ValidityState != ApiKey.Status.DELETED.ToString()).Select(r => KeyToken.FromEntity(r)).ToArray();
      });
      return keys;
    }

    ///<inheritdoc/>
    public int RegisteredKeyCount() {
      int cnt= 0;
      App.WithServiceScope(prov => {
        var repo= (IRepo<ApiKey>)prov.GetService(typeof(IRepo<ApiKey>));
        cnt= repo.AllUntracked.Where(r => r.ValidityState != ApiKey.Status.DELETED.ToString()).Count();
      });
      return cnt;
    }

    ///<inheritdoc/>
    public KeyToken VerifiedKey(string key) {
      //try to find token in cache and verify
      var token= cache[key];
      if (token != null)
        if (token.ValidFrom <= App.TimeInfo.Now
            && (!token.ValidUntil.HasValue || token.ValidUntil > App.TimeInfo.Now)
            && token.ValidityState == ApiKey.Status.ACTIVE.ToString())
          return token;
        else
          //not valid anymore -> remove from cache
          //we don't return here however in case the validity has changed but still check the database
          cache.Evict(key);

      //not found -> try to get from DB
      App.WithServiceScope(prov => {
        var um= (UserManager<User>)prov.GetService(typeof(UserManager<User>));
        var repo= (IRepo<ApiKey>)prov.GetService(typeof(IRepo<ApiKey>));
        token= KeyToken.FromEntity(repo.AllUntracked
                       .Where(r => r.ValidFrom <= App.TimeInfo.Now
                              && (r.ValidUntil == null || r.ValidUntil > App.TimeInfo.Now)
                              &&  r.ValidityState == ApiKey.Status.ACTIVE.ToString())
                       .FirstOrDefault(r => um.PasswordHasher.VerifyHashedPassword(null, r.Hash, key) == PasswordVerificationResult.Success));
      });

      //cache if existing
      if (token != null)
        cache[key]= token;

      return token;
    }

    ///<inheritdoc/>
    public string GenerateKey() {
      return generateRandomCryptographicKey(options.genKeyLength.HasValue ? options.genKeyLength.Value : 32);
    }

    ///<summary>
    ///Generates a string of the specified length by using the cryptographically
    ///secure <see cref="RNGCryptoServiceProvider"/> random number generator.
    ///The RNG will create an array of (non-zero) bytes. This will be returned as a B64 string
    ///</summary>
    private static string generateRandomCryptographicKey(int keyLength) {
      var rngCryptoServiceProvider = new RNGCryptoServiceProvider();
      byte[] randomBytes = new byte[keyLength];
      rngCryptoServiceProvider.GetNonZeroBytes(randomBytes);
      return Convert.ToBase64String(randomBytes);
    }

    ///<summary>Options for the registry</summary>
    public class Options {
      ///<summary>initialKey</summary>
      public string initialKey { get; set; }
      ///<summary>initialTokenName</summary>
      public string initialTokenName { get; set; }
      ///<summary>initialValidHours</summary>
      public int? initialValidHours { get; set; }
      ///<summary>genKeyLength</summary>
      public int? genKeyLength { get; set; }
    }
  }
}