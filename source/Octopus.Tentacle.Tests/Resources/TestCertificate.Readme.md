# Test Certificates Notes

Created a test certficiate authority for the first two certs.
makecert -n "CN=AwesomeCertAuth" -cy authority -a sha1 -sv "AwesomeCertAuthPrivateKey.pvk" -r "AwesomeCertAuth.cer"

## TestCertificateWithPassword.pfx
makecert -n "CN=OctopusTentacle" -ic "AwesomeCertAuth.cer" -iv "AwesomeCertAuthPrivateKey.pvk" -a sha1 -sky exchange -pe -sv "OctopusTentaclePrivateKey.pvk" "OctopusTentacle.cer"
pvk2pfx -pvk "OctopusTentaclePrivateKey.pvk" -spc "OctopusTentacle.cer" -pfx "OctopusTentacle.pfx" -pi Password01!

## TestCertificateNoPassword.pfx
makecert -n "CN=OctopusTentacleNoPassword" -ic "AwesomeCertAuth.cer" -iv "AwesomeCertAuthPrivateKey.pvk" -a sha1 -sky exchange -pe -sv "OctopusTentacleNoPasswordPrivateKey.pvk" "OctopusTentacleNoPassword.cer"
pvk2pfx -pvk "OctopusTentacleNoPasswordPrivateKey.pvk" -spc "OctopusTentacleNoPassword.cer" -pfx "OctopusTentacleNoPassword.pfx"

## TestCertificateNoPrivateKey.pfx

Used openssl to create a pfx w/o a private key.

* openssl req -x509 -newkey rsa:2048 -keyout TestCertificatePrivateKey.pem -out TestCertificate.pem -days 999 -subj '/CN=OctopusTestCertificate'
* openssl pkcs12 -export -nokeys -in TestCertificate.pem -out TestCertificateNoPrivateKey.pfx