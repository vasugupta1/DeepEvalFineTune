Feature: SCA Transactions API
    In order to authenticate customer payment transactions
    As a downstream payment service
    I want to start SCA transactions, issue authentication factors, and verify authentication outcomes

    Scenario: Health check endpoint returns healthy
        When I call the healthcheck endpoint
        Then the response status code should be 200

    Scenario: Start a high-value SCA transaction requires challenge and returns available factors
        Given an account number "12345678" and transaction amount 50.00
        When I start an SCA transaction
        Then the response status code should be 200
        And the transaction status should be "CHALLENGE_REQUIRED"
        And the transaction reason code should be "SCA_STEP_UP_REQUIRED"
        And the available factors should include "otp,email,biometric"

    Scenario: Start a low-value SCA transaction is exempt approved
        Given an account number "12345678" and transaction amount 15.00
        When I start an SCA transaction
        Then the response status code should be 200
        And the transaction status should be "EXEMPT_APPROVED"
        And the transaction reason code should be "LOW_VALUE_EXEMPTION"

    Scenario Outline: Issue a valid authentication factor for a started transaction succeeds
        Given an account number "87654321" and transaction amount 100.00
        When I start an SCA transaction
        And I issue the factor "<factor>" for account "87654321"
        Then the response status code should be 200
        And the transaction status should be "FACTOR_ISSUED"
        And the selected factor should be "<factor>"
        And the transaction reason code should be "<reasonCode>"

        Examples:
            | factor    | reasonCode              |
            | otp       | FACTOR_OTP_ISSUED       |
            | email     | FACTOR_EMAIL_ISSUED     |
            | biometric | FACTOR_BIOMETRIC_ISSUED |

    Scenario Outline: Complete SCA journey where start, issue, and verify all succeed
        Given an account number "99990000" and transaction amount 75.00
        When I start an SCA transaction
        And I issue the factor "<factor>" for account "99990000"
        And I verify the transaction for account "99990000"
        Then the response status code should be 200
        And the verification result should be authenticated true
        And the verification status should be "AUTHENTICATED"
        And the verification reason code should be "SCA_AUTH_SUCCESS"
        And the verified selected factor should be "<factor>"

        Examples:
            | factor    |
            | otp       |
            | email     |
            | biometric |

    Scenario: Low-value exempt SCA journey succeeds on verify without issuing a factor
        Given an account number "11112222" and transaction amount 20.00
        When I start an SCA transaction
        And I verify the transaction for account "11112222"
        Then the response status code should be 200
        And the verification result should be authenticated true
        And the verification status should be "AUTHENTICATED"
        And the verification reason code should be "LOW_VALUE_EXEMPTION_VERIFIED"

    Scenario: Journey where start and issue succeed but verify fails due to account number mismatch
        Given an account number "12345678" and transaction amount 120.00
        When I start an SCA transaction
        And I issue the factor "otp" for account "12345678"
        Then the response status code should be 200
        And the transaction status should be "FACTOR_ISSUED"
        When I verify the transaction for account "00009999"
        Then the response status code should be 200
        And the verification result should be authenticated false
        And the verification status should be "AUTH_FAILED"
        And the verification reason code should be "ACCOUNT_NUMBER_MISMATCH"

    Scenario: Journey where start and issue succeed but verify fails due to unknown transaction ID
        Given an account number "12345678" and transaction amount 90.00
        When I start an SCA transaction
        And I issue the factor "email" for account "12345678"
        Then the response status code should be 200
        And the transaction status should be "FACTOR_ISSUED"
        When I verify an unknown transaction ID for account "12345678"
        Then the response status code should be 404

    Scenario: Journey where start and issue succeed but verify fails due to empty account number
        Given an account number "12345678" and transaction amount 60.00
        When I start an SCA transaction
        And I issue the factor "biometric" for account "12345678"
        Then the response status code should be 200
        And the transaction status should be "FACTOR_ISSUED"
        When I verify the transaction for account ""
        Then the response status code should be 400

    Scenario: Journey where start succeeds for high-value transaction but verify fails when factor was not issued
        Given an account number "55556666" and transaction amount 85.00
        When I start an SCA transaction
        And I verify the transaction for account "55556666"
        Then the response status code should be 200
        And the verification result should be authenticated false
        And the verification status should be "AUTH_FAILED"
        And the verification reason code should be "SCA_FACTOR_NOT_ISSUED"
