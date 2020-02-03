using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Tlabs.Data;
using Tlabs.Data.Entity;
using Tlabs.Misc;
using Tlabs.Server.Model;

namespace Tlabs.Server.Auth {
  ///<inheritdoc/>
  public class DefaultApiKeyRegistry : IApiKeyRegistry {
    private static BasicCache<string, KeyToken> cache= new BasicCache<string, KeyToken>();

    ///<inheritdoc/>
    public KeyToken Register(string key, string tokenName, string description = null, DateTime? validUntil = null) {
      ApiKey apiKey= null;
      App.WithServiceScope(prov => {
        var um= (UserManager<User>)prov.GetService(typeof(UserManager<User>));
        //first, hash and store in db
        apiKey= new ApiKey {
          Hash= um.PasswordHasher.HashPassword(null, key),
          TokenName= tokenName,
          Description= description,
          ValidFrom= App.TimeInfo.Now,
          ValidUntil= validUntil,
          Roles= null, //TODO: roles
          ValidityState= ApiKey.Status.ACTIVE.ToString()
        };
        var repo= (IRepo<ApiKey>)prov.GetService(typeof(IRepo<ApiKey>));
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
      //TODO what happens if we have a load balancer or similar?
      KeyToken returnToken= null;
      App.WithServiceScope(prov => {
        var repo= (IRepo<ApiKey>)prov.GetService(typeof(IRepo<ApiKey>));
        var apiKey= repo.AllUntracked.FirstOrDefault(r => r.TokenName == tokenName);

        var token= cache.Entries.FirstOrDefault(r => r.Value.TokenName == tokenName);
        if (token.Key != null)
          cache.Evict(token.Key);
        if (apiKey == null || apiKey.ValidityState == ApiKey.Status.DELETED.ToString())
          return;

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
      return generateRandomCryptographicKey(32); //TODO make length configurable
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
  }
}