using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FixMyCity.DAL.DTOs
{
    public class CertificateIssuedResult
    {
        public int NewCertId { get; set; }
        public string VerificationCode { get; set; }
    }
}
