  using GBILET.Core.Service;
  
  namespace GBILET.Core.Helpers;

  public static class PnrGenerator
  {
      private const string Chars =
  "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
      private static readonly Random _random = new();

      public static async Task<string>
  GenerateUniqueAsync(IBookingRepository bookingRepository)
      {
          string pnr;
          do
          {
              pnr = GenerateRandom();
          }
          while (await
  bookingRepository.InternalPnrExistsAsync(pnr));

          return pnr;
      }

      private static string GenerateRandom()
      {
          return new string(Enumerable.Range(0, 6)
              .Select(_ => Chars[_random.Next(Chars.Length)])
              .ToArray());
      }
  }