using FixMyCity.DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FixMyCity.DAL.Repositories.Interfaces
{

    public interface IPaymentRepository
    {

        int CreateContribution(int complaintId, int citizenUserId,
                               decimal amount, string transactionRef);

        bool UpdatePaymentStatus(string transactionRef, string newStatus,
                                 string failureReason = null);

        List<Contribution> GetContributionsByComplaint(int complaintId);


        decimal GetFundingTotal(int complaintId);


        List<Contribution> GetContributionsByCitizen(int citizenUserId);
    }


}
