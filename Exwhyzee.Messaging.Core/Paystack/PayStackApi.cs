using System;
using System.Collections.Generic;
using System.Text;
using Exwhyzee.Messaging.Core.Paystack.Transactions;
using Exwhyzee.Messaging.Core.Paystack.Subscription;
using Exwhyzee.Messaging.Core.Paystack.Charge;
using imprtCustomer = Exwhyzee.Messaging.Core.Paystack.Customers;
using Exwhyzee.Messaging.Core.Paystack.Plans;
using Exwhyzee.Messaging.Core.Paystack.Transfers;
using Exwhyzee.Messaging.Core.Paystack.Subaccounts;
using Exwhyzee.Messaging.Core.Paystack.Verification;

namespace Exwhyzee.Messaging.Core.Paystack
{
    public interface IPayStackApi
    {
        ITransactions Transactions { get; }
        ISubscriptions Subscriptions { get; }
        ICharge Charge { get; }
        ICustomers Customers { get; }
        IPlans Plans { get; }
        ISubAccounts SubAccounts { get; }
        ITransfers Transfers { get; }
        IVerification Verification { get; }
        IChargeHelper CustomerCharge { get; }
    }


    public class PayStackApi : IPayStackApi
    {
        private ITransactions _transaction;
        private ISubscriptions _subscriptions;
        private ICharge _charge;
        private ICustomers _customers;
        private IPlans _plans;
        private ISubAccounts _subAccounts;
        private ITransfers _transfers;
        private IVerification _verification;
        private IChargeHelper _customerCharge;
        private string _secretKey;

        public PayStackApi()
        {
        }

        public PayStackApi(string secretKey)
        {
            _secretKey = secretKey;
        }

        public ITransactions Transactions
        {
            get
            {
                if (_transaction == null)
                    _transaction = new PaystackTransaction(_secretKey);
                return _transaction;
            }
        }

        public ISubscriptions Subscriptions
        {
            get
            {
                if (_subscriptions == null)
                    _subscriptions = new PaystackSubscription(_secretKey);
                return _subscriptions;
            }
        }

        public ICharge Charge
        {
            get
            {
                if (_charge == null) _charge = new PaystackCharge(_secretKey);

                return _charge;
            }
        }

        public ICustomers Customers
        {
            get
            {
                if (_customers == null) _customers = new imprtCustomer.PaystackCustomers(_secretKey);
                return _customers;
            }
        }

        public IPlans Plans
        {
            get
            {
                if (_plans == null) _plans = new PaystackPlans(_secretKey);
                return _plans;
            }
        }

        public ISubAccounts SubAccounts
        {
            get
            {
                if (_subAccounts == null) _subAccounts = new PaystackSubaccounts(_secretKey);
                return _subAccounts;
            }
        }

        public ITransfers Transfers
        {
            get
            {
                if (_transfers == null) _transfers = new PaystackTransfers(_secretKey);
                return _transfers;
            }
        }

        public IVerification Verification
        {
            get
            {
                if (_verification == null) _verification = new Exwhyzee.Messaging.Core.Paystack.Verification.Verification(_secretKey);

                return _verification;
            }
        }

        public IChargeHelper CustomerCharge
        {
            get
            {
                if (_customerCharge == null) _customerCharge = new Exwhyzee.Messaging.Core.Paystack.Fees.CustomerCharge();

                return _customerCharge;
            }
        }
    }
}

